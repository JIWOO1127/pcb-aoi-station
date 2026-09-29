package com.example.pcb_manage.recipe;

import static com.example.pcb_manage.recipe.RecipeDraftController.*;

import java.io.IOException;
import java.time.Duration;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import tools.jackson.databind.ObjectMapper;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.io.ByteArrayResource;
import org.springframework.core.io.ClassPathResource;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.client.BufferingClientHttpRequestFactory;
import org.springframework.http.client.SimpleClientHttpRequestFactory;
import org.springframework.http.client.MultipartBodyBuilder;
import org.springframework.stereotype.Component;
import org.springframework.web.client.ResourceAccessException;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientResponseException;
import org.springframework.web.multipart.MultipartFile;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

@Component
public class RecipeDraftClient {
    private static final Logger log = LoggerFactory.getLogger(RecipeDraftClient.class);
    private static final Map<String, Integer> REQUIRED_HOLES = Map.of(
            "ic_chip", 14,
            "resistance330", 2,
            "resistance470", 2,
            "capacitor", 2,
            "led", 2,
            "black_wire", 2,
            "red_wire", 2,
            "white_wire", 2);

    private final RecipeRepository repository;
    private final ObjectMapper objectMapper;
    private final RestClient restClient;
    private final String recipeDraftPath;
    private final boolean sampleFallbackEnabled;

    public RecipeDraftClient(RecipeRepository repository, ObjectMapper objectMapper,
            @Value("${app.recipe-draft.url:http://172.20.10.4:8001}") String baseUrl,
            @Value("${app.recipe-draft.path:/recipe-draft}") String recipeDraftPath,
            @Value("${app.recipe-draft.timeout-seconds:120}") int timeoutSeconds,
            @Value("${app.recipe-draft.sample-fallback-enabled:true}") boolean sampleFallbackEnabled) {
        this.repository = repository;
        this.objectMapper = objectMapper;
        this.sampleFallbackEnabled = sampleFallbackEnabled;
        this.recipeDraftPath = recipeDraftPath;
        var simpleRequestFactory = new SimpleClientHttpRequestFactory();
        simpleRequestFactory.setConnectTimeout(Duration.ofSeconds(timeoutSeconds));
        simpleRequestFactory.setReadTimeout(Duration.ofSeconds(timeoutSeconds));
        var requestFactory = new BufferingClientHttpRequestFactory(simpleRequestFactory);
        this.restClient = RestClient.builder().baseUrl(baseUrl).requestFactory(requestFactory).build();
    }

    public DraftResponse analyze(MultipartFile image, String productId) {
        String normalizedProductId = productId == null ? "" : productId.trim();
        validateInput(image, normalizedProductId);

        try {
            var imageResource = new ByteArrayResource(image.getBytes()) {
                @Override public String getFilename() { return image.getOriginalFilename(); }
            };
            var body = new MultipartBodyBuilder();
            body.part("image", imageResource)
                    .filename(image.getOriginalFilename())
                    .contentType(MediaType.parseMediaType(image.getContentType()));
            body.part("productId", normalizedProductId)
                    .contentType(MediaType.TEXT_PLAIN);

            DraftResponse response = restClient.post()
                    .uri(recipeDraftPath)
                    .contentType(MediaType.MULTIPART_FORM_DATA)
                    .body(body.build())
                    .retrieve()
                    .body(DraftResponse.class);
            if (response == null) throw new RecipeDraftException(HttpStatus.BAD_GATEWAY,
                    "RECIPE_DRAFT_EMPTY_RESPONSE", "Python 분석 응답이 비어 있습니다.");
            return applyValidation(new DraftResponse(normalizedProductId, response.parts(), response.overlay(),
                    response.warnings()));
        } catch (RestClientResponseException exception) {
            if (exception.getStatusCode().value() == 404 && sampleFallbackEnabled) {
                return sample(normalizedProductId);
            }
            throw pythonError(exception);
        } catch (ResourceAccessException | IOException exception) {
            log.error("Python recipe-draft request failed: {}", exception.getMessage(), exception);
            if (sampleFallbackEnabled) return sample(normalizedProductId);
            throw new RecipeDraftException(HttpStatus.BAD_GATEWAY, "RECIPE_DRAFT_UNAVAILABLE",
                    "Python recipe-draft 서비스에 연결할 수 없습니다.");
        }
    }

    private void validateInput(MultipartFile image, String productId) {
        if (!repository.productExists(productId)) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "PRODUCT_NOT_FOUND",
                    "등록된 활성 제품만 선택할 수 있습니다.");
        }
        if (image == null || image.isEmpty()) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "IMAGE_REQUIRED", "기준 보드 사진을 선택해 주세요.");
        }
        String contentType = image.getContentType();
        if (!MediaType.IMAGE_JPEG_VALUE.equals(contentType) && !MediaType.IMAGE_PNG_VALUE.equals(contentType)) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "INVALID_IMAGE_TYPE",
                    "JPEG 또는 PNG 사진만 업로드할 수 있습니다.");
        }
    }

    private RecipeDraftException pythonError(RestClientResponseException exception) {
        try {
            @SuppressWarnings("unchecked")
            Map<String, Object> error = objectMapper.readValue(exception.getResponseBodyAsByteArray(), Map.class);
            String code = String.valueOf(error.getOrDefault("code", "RECIPE_DRAFT_ERROR"));
            String message = String.valueOf(error.getOrDefault("message", "사진 분석에 실패했습니다."));
            HttpStatus status = HttpStatus.resolve(exception.getStatusCode().value());
            return new RecipeDraftException(status == null ? HttpStatus.BAD_GATEWAY : status, code, message);
        } catch (Exception ignored) {
            return new RecipeDraftException(HttpStatus.BAD_GATEWAY, "RECIPE_DRAFT_ERROR", "사진 분석에 실패했습니다.");
        }
    }

    private DraftResponse sample(String productId) {
        try {
            DraftResponse response = objectMapper.readValue(
                    new ClassPathResource("recipe-draft-actual.json").getInputStream(), DraftResponse.class);
            List<String> warnings = new ArrayList<>(response.warnings() == null ? List.of() : response.warnings());
            warnings.add("Python /recipe-draft 연결 실패로 실제 응답 샘플을 사용했습니다.");
            return applyValidation(new DraftResponse(productId, response.parts(), response.overlay(), warnings));
        } catch (IOException exception) {
            throw new RecipeDraftException(HttpStatus.BAD_GATEWAY, "RECIPE_DRAFT_UNAVAILABLE",
                    "Python recipe-draft 서비스에 연결할 수 없습니다.");
        }
    }

    private DraftResponse applyValidation(DraftResponse response) {
        List<DraftPart> parts = response.parts() == null ? List.of() : response.parts();
        Map<String, Integer> holeUses = new HashMap<>();
        parts.forEach(part -> safe(part.holes()).forEach(hole -> holeUses.merge(hole, 1, Integer::sum)));

        List<DraftPart> validated = parts.stream().map(part -> {
            Set<String> reasons = new HashSet<>(safe(part.reasons()));
            double confidence = part.confidence() == null ? 0 : part.confidence();
            if (confidence < 0.5) reasons.add("LOW_CONFIDENCE");
            Integer expected = REQUIRED_HOLES.get(part.detectedClass());
            if (expected == null || safe(part.holes()).size() != expected) reasons.add("HOLE_COUNT");
            if (safe(part.holes()).stream().anyMatch(hole -> holeUses.getOrDefault(hole, 0) > 1)) {
                reasons.add("HOLE_OVERLAP");
            }
            List<String> ordered = List.of("LOW_CONFIDENCE", "HOLE_COUNT", "HOLE_OVERLAP").stream()
                    .filter(reasons::contains).toList();
            return new DraftPart(part.partId(), part.detectedClass(), safe(part.holes()), part.confidence(),
                    !ordered.isEmpty(), ordered, part.box());
        }).toList();
        return new DraftResponse(response.productId(), validated, response.overlay(),
                response.warnings() == null ? List.of() : response.warnings());
    }

    private static <T> List<T> safe(List<T> values) { return values == null ? List.of() : values; }
}
