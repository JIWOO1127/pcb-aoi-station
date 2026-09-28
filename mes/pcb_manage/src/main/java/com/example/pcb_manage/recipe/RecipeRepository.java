package com.example.pcb_manage.recipe;

import static com.example.pcb_manage.recipe.RecipeController.*;

import java.time.OffsetDateTime;
import java.util.Arrays;
import java.util.List;
import java.util.UUID;

import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

@Repository
public class RecipeRepository {
    private final JdbcClient jdbc;
    public RecipeRepository(JdbcClient jdbc) { this.jdbc = jdbc; }
    private final org.springframework.jdbc.core.RowMapper<RecipeSummary> mapper = (rs,n) -> new RecipeSummary(
            rs.getObject("recipe_id", UUID.class), rs.getString("product_id"), rs.getInt("version"),
            rs.getString("note"), rs.getBoolean("active"), rs.getObject("created_at", OffsetDateTime.class));

    public List<RecipeSummary> list() { return jdbc.sql("SELECT recipe_id,product_id,version,note,active,created_at FROM recipe ORDER BY product_id,version DESC").query(mapper).list(); }
    public RecipeSummary get(UUID id) { return jdbc.sql("SELECT recipe_id,product_id,version,note,active,created_at FROM recipe WHERE recipe_id=:id").param("id",id).query(mapper).optional().orElse(null); }
    public List<RecipePartResponse> parts(UUID id) { return jdbc.sql("SELECT part_id,expected_class,holes FROM recipe_part WHERE recipe_id=:id ORDER BY part_id").param("id",id).query((rs,n) -> new RecipePartResponse(rs.getString("part_id"),rs.getString("expected_class"),parseJson(rs.getString("holes")))).list(); }
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
