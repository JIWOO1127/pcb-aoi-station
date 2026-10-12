package com.example.pcb_manage.recipe;

import java.util.List;

import com.example.pcb_manage.auth.JwtAuthenticationFilter.AuthenticatedUser;
import com.fasterxml.jackson.annotation.JsonProperty;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.multipart.MultipartFile;

@RestController
@RequestMapping("/api/recipe-drafts")
public class RecipeDraftController {
    private final RecipeDraftClient client;
    private final RecipeService recipeService;

    public RecipeDraftController(RecipeDraftClient client, RecipeService recipeService) {
        this.client = client;
        this.recipeService = recipeService;
    }

    @PostMapping(consumes = MediaType.MULTIPART_FORM_DATA_VALUE)
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public DraftResponse analyze(@RequestParam("image") MultipartFile image,
            @RequestParam("productId") String productId) {
        return client.analyze(image, productId);
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public RecipeController.RecipeDetail save(@Valid @RequestBody DraftSaveRequest request,
            @AuthenticationPrincipal AuthenticatedUser actor) {
        return recipeService.createDraft(request, actor.employeeId());
    }

    public record DraftResponse(String productId, List<DraftPart> parts, Overlay overlay,
            List<String> warnings) {}
    public record DraftPart(@NotBlank @Size(max = 50) String partId,
            @JsonProperty("class") String detectedClass, List<String> holes, Double confidence,
            boolean needsReview, List<String> reasons, Box box) {}
    public record Box(Integer x, Integer y, Integer w, Integer h) {}
    public record Overlay(String image, Integer width, Integer height, List<HolePoint> holes) {}
    public record HolePoint(String label, Double x, Double y) {}

    public record DraftSaveRequest(@NotBlank @Size(max = 50) String productId,
            @Size(max = 2000) String note,
            @NotEmpty @Size(max = 200) List<@Valid DraftSavePart> parts) {}
    public record DraftSavePart(@NotBlank @Size(max = 50) String partId,
            @NotBlank @Size(max = 100) @JsonProperty("class") String detectedClass,
            @NotEmpty @Size(max = 20) List<String> holes,
            Double confidence, boolean needsReview, boolean reviewConfirmed,
            List<String> reasons) {}
}
