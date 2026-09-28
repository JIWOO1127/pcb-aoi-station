package com.example.pcb_manage.inspection;

import static com.example.pcb_manage.inspection.InspectionController.*;

import java.time.OffsetDateTime;
import java.util.Arrays;
import java.util.List;
import java.util.UUID;
import java.util.stream.Collectors;

import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

@Repository
public class InspectionRepository {
    private final JdbcClient jdbc;
    public InspectionRepository(JdbcClient jdbc) { this.jdbc = jdbc; }

    public boolean inspectionExists(UUID id) { return count("inspection", "inspection_id", id) > 0; }
    public boolean confirmationExists(UUID id) { return count("confirmation", "confirmation_id", id) > 0; }
    private int count(String table, String column, UUID id) {
        return jdbc.sql("SELECT COUNT(*) FROM " + table + " WHERE " + column + "=:id")
                .param("id", id).query(Integer.class).single();
    }
    public boolean productExists(String id) { return jdbc.sql("SELECT COUNT(*) FROM product WHERE product_id=:id AND active=true").param("id", id).query(Integer.class).single() > 0; }
    public boolean workerExists(String id) { return jdbc.sql("SELECT COUNT(*) FROM worker WHERE worker_id=:id AND active=true").param("id", id).query(Integer.class).single() > 0; }
    public boolean recipeMatches(UUID recipeId, String productId) { return jdbc.sql("SELECT COUNT(*) FROM recipe WHERE recipe_id=:id AND product_id=:productId").param("id", recipeId).param("productId", productId).query(Integer.class).single() > 0; }

    public void insert(CreateInspectionRequest r) {
        jdbc.sql("""
                INSERT INTO inspection(inspection_id,product_id,recipe_id,worker_id,verdict,defect_count,
                    latency_ms,captured_at,station_id,error_code)
                VALUES(:id,:productId,:recipeId,:workerId,:verdict,:defectCount,:latencyMs,:capturedAt,:stationId,:errorCode)
                """).param("id", r.inspectionId()).param("productId", r.productId()).param("recipeId", r.recipeId())
                .param("workerId", r.workerId()).param("verdict", r.verdict()).param("defectCount", r.defectCount())
                .param("latencyMs", r.latencyMs()).param("capturedAt", r.capturedAt()).param("stationId", r.stationId())
                .param("errorCode", r.errorCode()).update();
    }
    public void insertPart(UUID inspectionId, PartRequest p) {
        jdbc.sql("""
                INSERT INTO part_result(part_result_id,inspection_id,part_id,detected_class,defect_type,
                    expected_class,expected_holes,covered_holes)
                VALUES(:resultId,:inspectionId,:partId,:detectedClass,:defectType,:expectedClass,
                    CAST(:expectedHoles AS JSONB),CAST(:coveredHoles AS JSONB))
                """).param("resultId", UUID.randomUUID()).param("inspectionId", inspectionId).param("partId", p.partId())
                .param("detectedClass", p.detectedClass()).param("defectType", p.defectType()).param("expectedClass", p.expectedClass())
                .param("expectedHoles", json(p.expectedHoles())).param("coveredHoles", json(p.coveredHoles())).update();
    }
    public List<InspectionSummary> list(String verdict, String productId, String stationId) {
        return jdbc.sql("""
                SELECT i.inspection_id,i.recipe_id,r.version AS recipe_version,i.station_id,i.product_id,i.worker_id,
                       i.verdict,i.defect_count,i.error_code,i.latency_ms,i.captured_at
                FROM inspection i JOIN recipe r ON r.recipe_id=i.recipe_id
                WHERE (:verdict IS NULL OR i.verdict=:verdict)
                  AND (:productId IS NULL OR i.product_id=:productId)
                  AND (:stationId IS NULL OR i.station_id=:stationId)
                ORDER BY i.captured_at DESC LIMIT 500
                """).param("verdict", blankToNull(verdict)).param("productId", blankToNull(productId)).param("stationId", blankToNull(stationId))
                .query((rs, n) -> new InspectionSummary(rs.getObject("inspection_id", UUID.class), rs.getObject("recipe_id", UUID.class),
                        rs.getInt("recipe_version"), rs.getString("station_id"), rs.getString("product_id"), rs.getString("worker_id"),
                        rs.getString("verdict"), rs.getInt("defect_count"), rs.getString("error_code"), rs.getInt("latency_ms"),
                        rs.getObject("captured_at", OffsetDateTime.class))).list();
    }
    public InspectionSummary getSummary(UUID id) { return jdbc.sql("""
            SELECT i.inspection_id,i.recipe_id,r.version AS recipe_version,i.station_id,i.product_id,i.worker_id,
                   i.verdict,i.defect_count,i.error_code,i.latency_ms,i.captured_at
            FROM inspection i JOIN recipe r ON r.recipe_id=i.recipe_id WHERE i.inspection_id=:id
            """).param("id", id).query((rs,n) -> new InspectionSummary(rs.getObject("inspection_id", UUID.class), rs.getObject("recipe_id", UUID.class), rs.getInt("recipe_version"), rs.getString("station_id"), rs.getString("product_id"), rs.getString("worker_id"), rs.getString("verdict"), rs.getInt("defect_count"), rs.getString("error_code"), rs.getInt("latency_ms"), rs.getObject("captured_at", OffsetDateTime.class))).optional().orElse(null); }
    public List<PartResponse> parts(UUID id) { return jdbc.sql("SELECT part_id,expected_class,detected_class,defect_type,expected_holes,covered_holes FROM part_result WHERE inspection_id=:id ORDER BY part_id").param("id",id).query((rs,n) -> new PartResponse(rs.getString("part_id"),rs.getString("expected_class"),rs.getString("detected_class"),rs.getString("defect_type"),parseJson(rs.getString("expected_holes")),parseJson(rs.getString("covered_holes")))).list(); }
    public List<ConfirmationResponse> confirmations(UUID id) { return jdbc.sql("SELECT confirmation_id,worker_id,decision,reason_code,confirmed_at FROM confirmation WHERE inspection_id=:id ORDER BY confirmed_at").param("id",id).query((rs,n) -> new ConfirmationResponse(rs.getObject("confirmation_id",UUID.class),rs.getString("worker_id"),rs.getString("decision"),rs.getString("reason_code"),rs.getObject("confirmed_at",OffsetDateTime.class))).list(); }
    public void insertConfirmation(UUID inspectionId, ConfirmationRequest r) { jdbc.sql("INSERT INTO confirmation(confirmation_id,inspection_id,worker_id,decision,reason_code,confirmed_at) VALUES(:id,:inspectionId,:workerId,:decision,:reasonCode,:confirmedAt)").param("id",r.confirmationId()).param("inspectionId",inspectionId).param("workerId",r.workerId()).param("decision",r.decision()).param("reasonCode",r.reasonCode()).param("confirmedAt",r.confirmedAt()).update(); }

    private static String blankToNull(String value) { return value == null || value.isBlank() ? null : value; }
    private static String json(List<String> values) { return values.stream().map(v -> "\"" + v + "\"").collect(Collectors.joining(",", "[", "]")); }
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
