package com.example.pcb_manage.recipe;

import static com.example.pcb_manage.recipe.RecipeController.*;
import static com.example.pcb_manage.recipe.RecipeDraftController.*;

import java.time.OffsetDateTime;
import java.util.Arrays;
import java.util.List;
import java.util.UUID;
import java.util.stream.Collectors;

import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

@Repository
public class RecipeRepository {
    private final JdbcClient jdbc;
    public RecipeRepository(JdbcClient jdbc) { this.jdbc = jdbc; }

    private final org.springframework.jdbc.core.RowMapper<RecipeSummary> mapper = (rs, n) -> new RecipeSummary(
            rs.getObject("recipe_id", UUID.class), rs.getString("product_id"), rs.getInt("version"),
            rs.getString("note"), rs.getBoolean("active"), rs.getString("status"),
            rs.getString("created_by"), rs.getString("approved_by"),
            rs.getObject("approved_at", OffsetDateTime.class),
            rs.getObject("created_at", OffsetDateTime.class));

    public List<RecipeSummary> list() {
        return jdbc.sql("""
                SELECT recipe_id,product_id,version,note,active,status,created_by,approved_by,approved_at,created_at
                FROM recipe ORDER BY product_id,version DESC
                """).query(mapper).list();
    }

    public RecipeSummary get(UUID id) {
        return jdbc.sql("""
                SELECT recipe_id,product_id,version,note,active,status,created_by,approved_by,approved_at,created_at
                FROM recipe WHERE recipe_id=:id
                """).param("id", id).query(mapper).optional().orElse(null);
    }

    public List<RecipePartResponse> parts(UUID id) {
        return jdbc.sql("""
                SELECT part_id,expected_class,holes,confidence,needs_review,review_confirmed,review_reasons
                FROM recipe_part WHERE recipe_id=:id ORDER BY part_id
                """).param("id", id).query((rs, n) -> new RecipePartResponse(
                        rs.getString("part_id"), rs.getString("expected_class"), parseJson(rs.getString("holes")),
                        rs.getBigDecimal("confidence") == null ? null : rs.getBigDecimal("confidence").doubleValue(), rs.getBoolean("needs_review"),
                        rs.getBoolean("review_confirmed"), parseJson(rs.getString("review_reasons")))).list();
    }

    public boolean productExists(String productId) {
        return jdbc.sql("SELECT COUNT(*) FROM product WHERE product_id=:productId AND active=true")
                .param("productId", productId).query(Integer.class).single() > 0;
    }

    public boolean versionExists(String productId, int version) {
        return jdbc.sql("SELECT COUNT(*) FROM recipe WHERE product_id=:productId AND version=:version")
                .param("productId", productId).param("version", version).query(Integer.class).single() > 0;
    }

    public int nextVersion(String productId) {
        return jdbc.sql("SELECT COALESCE(MAX(version),0)+1 FROM recipe WHERE product_id=:productId")
                .param("productId", productId).query(Integer.class).single();
    }

    public void deactivateForProduct(String productId) {
        jdbc.sql("""
                UPDATE recipe SET active=false,
                    status=CASE WHEN status='ACTIVE' THEN 'ARCHIVED' ELSE status END
                WHERE product_id=:productId AND active=true
                """).param("productId", productId).update();
    }

    public void insert(UUID recipeId, CreateRecipeRequest request, String actorId) {
        jdbc.sql("""
                INSERT INTO recipe(recipe_id,product_id,version,note,active,status,created_by,approved_by,approved_at)
                VALUES(:id,:productId,:version,:note,true,'ACTIVE',:actor,:actor,CURRENT_TIMESTAMP)
                """).param("id", recipeId).param("productId", request.productId())
                .param("version", request.version()).param("note", request.note()).param("actor", actorId).update();
    }

    public void insertDraft(UUID recipeId, String productId, int version, String note, String actorId) {
        jdbc.sql("""
                INSERT INTO recipe(recipe_id,product_id,version,note,active,status,created_by)
                VALUES(:id,:productId,:version,:note,false,'DRAFT',:actor)
                """).param("id", recipeId).param("productId", productId).param("version", version)
                .param("note", note).param("actor", actorId).update();
    }

    public void insertPart(UUID recipeId, CreateRecipePartRequest part) {
        jdbc.sql("""
                INSERT INTO recipe_part(recipe_id,part_id,expected_class,holes)
                VALUES(:recipeId,:partId,:expectedClass,CAST(:holes AS JSONB))
                """).param("recipeId", recipeId).param("partId", part.partId())
                .param("expectedClass", part.expectedClass()).param("holes", json(part.holes())).update();
    }

    public void insertDraftPart(UUID recipeId, DraftSavePart part) {
        jdbc.sql("""
                INSERT INTO recipe_part(recipe_id,part_id,expected_class,holes,confidence,needs_review,review_confirmed,review_reasons)
                VALUES(:recipeId,:partId,:expectedClass,CAST(:holes AS JSONB),:confidence,:needsReview,:reviewConfirmed,CAST(:reasons AS JSONB))
                """).param("recipeId", recipeId).param("partId", part.partId())
                .param("expectedClass", part.detectedClass()).param("holes", json(part.holes()))
                .param("confidence", part.confidence()).param("needsReview", part.needsReview())
                .param("reviewConfirmed", part.reviewConfirmed()).param("reasons", json(part.reasons())).update();
    }

    public void activate(UUID recipeId, String actorId) {
        jdbc.sql("""
                UPDATE recipe SET active=true,status='ACTIVE',approved_by=:actor,approved_at=CURRENT_TIMESTAMP
                WHERE recipe_id=:id
                """).param("id", recipeId).param("actor", actorId).update();
    }

    private static String json(List<String> values) {
        if (values == null) return "[]";
        return values.stream().map(value -> "\"" + value.replace("\\", "\\\\").replace("\"", "\\\"") + "\"")
                .collect(Collectors.joining(",", "[", "]"));
    }

    private static List<String> parseJson(String value) {
        if (value == null) return List.of();
        String normalized = value.trim();
        if (normalized.startsWith("\"") && normalized.endsWith("\"")) {
            normalized = normalized.substring(1, normalized.length() - 1)
                    .replace("\\\"", "\"").replace("\\\\", "\\");
        }
        if (normalized.equals("[]")) return List.of();
        String body = normalized.substring(1, normalized.length() - 1);
        return Arrays.stream(body.split(",")).map(v -> v.trim().replace("\"", "")).toList();
    }
}
