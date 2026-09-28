package com.example.pcb_manage.recipe;

import static com.example.pcb_manage.recipe.RecipeController.*;

import java.util.HashSet;
import java.util.UUID;

import com.example.pcb_manage.admin.AuditLogRepository;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

@Service
public class RecipeService {
    private final RecipeRepository repository;
    private final AuditLogRepository auditLogRepository;

    public RecipeService(RecipeRepository repository, AuditLogRepository auditLogRepository) {
        this.repository = repository;
        this.auditLogRepository = auditLogRepository;
    }

    @Transactional
    public RecipeDetail create(CreateRecipeRequest request, String actorId) {
        if (!repository.productExists(request.productId())) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "사용 가능한 제품 ID가 아닙니다.");
        }
        if (repository.versionExists(request.productId(), request.version())) {
            throw new ResponseStatusException(HttpStatus.CONFLICT, "이미 존재하는 제품 버전입니다.");
        }
        var partIds = new HashSet<String>();
        for (CreateRecipePartRequest part : request.parts()) {
            if (!partIds.add(part.partId())) {
                throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "부품 ID는 중복될 수 없습니다: " + part.partId());
            }
            if (new HashSet<>(part.holes()).size() != part.holes().size()) {
                throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "같은 부품에 홀을 중복 입력할 수 없습니다: " + part.partId());
            }
        }

        UUID recipeId = UUID.randomUUID();
        repository.deactivateForProduct(request.productId());
        repository.insert(recipeId, request);
        request.parts().forEach(part -> repository.insertPart(recipeId, part));
        auditLogRepository.append(actorId, "RECIPE_CREATE", "RECIPE", recipeId.toString(),
                "productId=" + request.productId() + ", version=" + request.version()
                        + ", parts=" + request.parts().size());

        RecipeSummary summary = repository.get(recipeId);
        return new RecipeDetail(summary.recipeId(), summary.productId(), summary.version(), summary.note(),
                summary.active(), summary.createdAt(), repository.parts(recipeId));
    }
}
