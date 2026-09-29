package com.example.pcb_manage.recipe;

import static com.example.pcb_manage.recipe.RecipeController.*;
import static com.example.pcb_manage.recipe.RecipeDraftController.*;

import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.regex.Pattern;

import com.example.pcb_manage.admin.AuditLogRepository;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

@Service
public class RecipeService {
    private static final Pattern HOLE = Pattern.compile("^[A-U](1[0-3]|[0-9])$");
    private static final Map<String, Integer> REQUIRED_HOLES = Map.of(
            "ic_chip", 14, "resistance330", 2, "resistance470", 2, "capacitor", 2,
            "led", 2, "black_wire", 2, "red_wire", 2, "white_wire", 2);

    private final RecipeRepository repository;
    private final AuditLogRepository auditLogRepository;

    public RecipeService(RecipeRepository repository, AuditLogRepository auditLogRepository) {
        this.repository = repository;
        this.auditLogRepository = auditLogRepository;
    }

    @Transactional
    public RecipeDetail create(CreateRecipeRequest request, String actorId) {
        requireProduct(request.productId());
        if (repository.versionExists(request.productId(), request.version())) {
            throw new ResponseStatusException(HttpStatus.CONFLICT, "이미 존재하는 제품 버전입니다.");
        }
        validatePartIds(request.parts().stream().map(CreateRecipePartRequest::partId).toList());
        request.parts().forEach(part -> requireUniqueHoles(part.partId(), part.holes()));

        UUID recipeId = UUID.randomUUID();
        repository.deactivateForProduct(request.productId());
        repository.insert(recipeId, request, actorId);
        request.parts().forEach(part -> repository.insertPart(recipeId, part));
        auditLogRepository.append(actorId, "RECIPE_CREATE", "RECIPE", recipeId.toString(),
                "productId=" + request.productId() + ", version=" + request.version()
                        + ", parts=" + request.parts().size());
        return detail(recipeId);
    }

    @Transactional
    public RecipeDetail createDraft(DraftSaveRequest request, String actorId) {
        requireProduct(request.productId());
        validatePartIds(request.parts().stream().map(DraftSavePart::partId).toList());
        request.parts().forEach(part -> {
            requireAllowedClass(part.partId(), part.detectedClass());
            requireUniqueHoles(part.partId(), part.holes());
            if (part.holes().stream().anyMatch(hole -> !HOLE.matcher(hole).matches())) {
                throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "INVALID_HOLE",
                        part.partId() + "의 홀은 A0~U13 형식이어야 합니다.");
            }
        });

        int version = repository.nextVersion(request.productId());
        UUID recipeId = UUID.randomUUID();
        repository.insertDraft(recipeId, request.productId(), version, request.note(), actorId);
        request.parts().forEach(part -> repository.insertDraftPart(recipeId, part));
        auditLogRepository.append(actorId, "RECIPE_DRAFT_CREATE", "RECIPE", recipeId.toString(),
                "productId=" + request.productId() + ", version=" + version
                        + ", parts=" + request.parts().size());
        return detail(recipeId);
    }

    @Transactional
    public RecipeDetail approve(UUID recipeId, String actorId) {
        RecipeSummary recipe = repository.get(recipeId);
        if (recipe == null) throw new ResponseStatusException(HttpStatus.NOT_FOUND, "레시피를 찾을 수 없습니다.");
        if (!"DRAFT".equals(recipe.status())) {
            throw new RecipeDraftException(HttpStatus.CONFLICT, "RECIPE_NOT_DRAFT", "초안 상태의 레시피만 승인할 수 있습니다.");
        }
        List<RecipePartResponse> parts = repository.parts(recipeId);
        validateForApproval(parts);

        repository.deactivateForProduct(recipe.productId());
        repository.activate(recipeId, actorId);
        auditLogRepository.append(actorId, "RECIPE_APPROVE", "RECIPE", recipeId.toString(),
                "productId=" + recipe.productId() + ", version=" + recipe.version()
                        + ", parts=" + parts.size());
        return detail(recipeId);
    }

    public RecipeDetail detail(UUID recipeId) {
        RecipeSummary summary = repository.get(recipeId);
        if (summary == null) throw new ResponseStatusException(HttpStatus.NOT_FOUND, "레시피를 찾을 수 없습니다.");
        return new RecipeDetail(summary.recipeId(), summary.productId(), summary.version(), summary.note(),
                summary.active(), summary.status(), summary.createdBy(), summary.approvedBy(), summary.approvedAt(),
                summary.createdAt(), repository.parts(recipeId));
    }

    private void validateForApproval(List<RecipePartResponse> parts) {
        if (parts.isEmpty()) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "PART_REQUIRED", "부품이 한 개 이상 필요합니다.");
        }
        Map<String, String> holeOwners = new HashMap<>();
        for (RecipePartResponse part : parts) {
            requireAllowedClass(part.partId(), part.expectedClass());
            if (part.needsReview() && !part.reviewConfirmed()) {
                throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "REVIEW_REQUIRED",
                        part.partId() + " 부품을 확인하거나 삭제해 주세요.");
            }
            int expectedCount = REQUIRED_HOLES.get(part.expectedClass());
            if (part.holes().size() != expectedCount) {
                throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "HOLE_COUNT",
                        part.partId() + "의 홀 개수가 " + expectedCount + "개여야 합니다.");
            }
            for (String hole : part.holes()) {
                if (!HOLE.matcher(hole).matches()) {
                    throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "INVALID_HOLE",
                            part.partId() + "의 홀은 A0~U13 형식이어야 합니다.");
                }
                String owner = holeOwners.putIfAbsent(hole, part.partId());
                if (owner != null) {
                    throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "HOLE_OVERLAP",
                            hole + " 홀이 " + owner + "과 " + part.partId() + "에 중복 지정되었습니다.");
                }
            }
        }
    }

    private void requireProduct(String productId) {
        if (!repository.productExists(productId)) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "PRODUCT_NOT_FOUND", "등록된 활성 제품만 선택할 수 있습니다.");
        }
    }

    private void requireAllowedClass(String partId, String partClass) {
        if (!REQUIRED_HOLES.containsKey(partClass)) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "INVALID_CLASS",
                    partId + "의 class가 허용 목록에 없습니다.");
        }
    }

    private void validatePartIds(List<String> partIds) {
        var seen = new HashSet<String>();
        for (String partId : partIds) {
            if (!seen.add(partId)) {
                throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "DUPLICATE_PART_ID",
                        "부품 ID는 중복될 수 없습니다: " + partId);
            }
        }
    }

    private void requireUniqueHoles(String partId, List<String> holes) {
        if (new HashSet<>(holes).size() != holes.size()) {
            throw new RecipeDraftException(HttpStatus.BAD_REQUEST, "DUPLICATE_HOLE",
                    "같은 부품에 중복된 홀이 있습니다: " + partId);
        }
    }
}
