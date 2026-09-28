package com.example.pcb_manage.recipe;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import com.example.pcb_manage.auth.JwtAuthenticationFilter.AuthenticatedUser;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

@RestController
@RequestMapping("/api/recipes")
public class RecipeController {
    private final RecipeRepository repository;
    private final RecipeService service;
    public RecipeController(RecipeRepository repository, RecipeService service) {
        this.repository = repository;
        this.service = service;
    }

    @GetMapping
    public List<RecipeSummary> list() { return repository.list(); }

    @GetMapping("/{recipeId}")
    public RecipeDetail get(@PathVariable UUID recipeId) {
        RecipeSummary summary = repository.get(recipeId);
        if (summary == null) throw new ResponseStatusException(HttpStatus.NOT_FOUND, "레시피를 찾을 수 없습니다.");
        return new RecipeDetail(summary.recipeId(), summary.productId(), summary.version(), summary.note(),
                summary.active(), summary.createdAt(), repository.parts(recipeId));
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public RecipeDetail create(@Valid @RequestBody CreateRecipeRequest request,
            @AuthenticationPrincipal AuthenticatedUser actor) {
        return service.create(request, actor.employeeId());
    }

    public record RecipeSummary(UUID recipeId, String productId, int version, String note,
            boolean active, OffsetDateTime createdAt) {}
    public record RecipePartResponse(String partId, String expectedClass, List<String> holes) {}
    public record RecipeDetail(UUID recipeId, String productId, int version, String note,
            boolean active, OffsetDateTime createdAt, List<RecipePartResponse> parts) {}
    public record CreateRecipeRequest(
            @NotBlank @Size(max = 50) String productId,
            @Positive int version,
            @Size(max = 2000) String note,
            @NotEmpty @Size(max = 200) List<@Valid CreateRecipePartRequest> parts) {}
    public record CreateRecipePartRequest(
            @NotBlank @Size(max = 50) String partId,
            @NotBlank @Size(max = 100) String expectedClass,
            @NotEmpty @Size(max = 20) List<
                    @Pattern(regexp = "[A-U](?:[0-9]|1[0-3])", message = "홀은 A0~U13 형식이어야 합니다.") String> holes) {}
}
