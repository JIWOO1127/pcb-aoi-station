package com.example.pcb_manage.recipe;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

@RestController
@RequestMapping("/api/recipes")
public class RecipeController {
    private final RecipeRepository repository;
    public RecipeController(RecipeRepository repository) { this.repository = repository; }

    @GetMapping
    public List<RecipeSummary> list() { return repository.list(); }

    @GetMapping("/{recipeId}")
    public RecipeDetail get(@PathVariable UUID recipeId) {
        RecipeSummary summary = repository.get(recipeId);
        if (summary == null) throw new ResponseStatusException(HttpStatus.NOT_FOUND, "레시피를 찾을 수 없습니다.");
        return new RecipeDetail(summary.recipeId(), summary.productId(), summary.version(), summary.note(),
                summary.active(), summary.createdAt(), repository.parts(recipeId));
    }

    public record RecipeSummary(UUID recipeId, String productId, int version, String note,
            boolean active, OffsetDateTime createdAt) {}
    public record RecipePartResponse(String partId, String expectedClass, List<String> holes) {}
    public record RecipeDetail(UUID recipeId, String productId, int version, String note,
            boolean active, OffsetDateTime createdAt, List<RecipePartResponse> parts) {}
}
