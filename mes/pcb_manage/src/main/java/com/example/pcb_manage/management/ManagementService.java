package com.example.pcb_manage.management;

import static com.example.pcb_manage.management.ManagementController.*;

import java.math.BigDecimal;
import java.math.RoundingMode;
import java.sql.Date;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import org.springframework.http.HttpStatus;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

@Service
public class ManagementService {
    private static final ZoneOffset KST = ZoneOffset.ofHours(9);
    private static final List<String> DEFAULT_CLASSES = List.of("black_wire", "capacitor", "ic_chip", "led",
            "red_wire", "resistance330", "resistance470", "white_wire");
    private final JdbcClient jdbc;

    public ManagementService(JdbcClient jdbc) { this.jdbc = jdbc; }

    public List<ProductResponse> products() {
        return jdbc.sql("""
                SELECT p.product_id,p.name,p.description,p.active,p.created_at,
                       (SELECT MAX(r.version) FROM recipe r WHERE r.product_id=p.product_id AND r.active=true) recipe_version
                FROM product p ORDER BY p.product_id
                """).query((rs,n) -> new ProductResponse(rs.getString("product_id"), rs.getString("name"),
                        rs.getString("description"), rs.getObject("recipe_version") == null ? "-" : "v" + rs.getInt("recipe_version"),
                        rs.getObject("created_at", OffsetDateTime.class), rs.getBoolean("active") ? "ACTIVE" : "INACTIVE")).list();
    }

    public ProductResponse product(String id) {
        return products().stream().filter(item -> item.id().equals(id)).findFirst()
                .orElseThrow(() -> notFound("제품을 찾을 수 없습니다."));
    }

    @Transactional
    public ProductResponse createProduct(ProductRequest request) {
        if (count("product", "product_id", request.id()) > 0) throw conflict("이미 존재하는 제품 ID입니다.");
        jdbc.sql("INSERT INTO product(product_id,name,description,active) VALUES(:id,:name,:description,true)")
                .param("id", request.id()).param("name", request.name()).param("description", request.description()).update();
        return product(request.id());
    }

    @Transactional
    public ProductResponse updateProduct(String id, ProductUpdateRequest request) {
        if (count("product", "product_id", id) == 0) throw notFound("제품을 찾을 수 없습니다.");
        jdbc.sql("UPDATE product SET name=:name,description=:description,active=:active,updated_at=CURRENT_TIMESTAMP WHERE product_id=:id")
                .param("id", id).param("name", request.name()).param("description", request.description())
                .param("active", request.active()).update();
        return product(id);
    }

    public List<StationResponse> stations() {
        OffsetDateTime start = LocalDate.now(KST).atStartOfDay().atOffset(KST);
        return jdbc.sql("""
                SELECT s.station_id,s.name,s.status,s.product_id,r.version,s.camera_status,s.ai_status,s.mes_status,s.last_seen,
                       (SELECT COUNT(*) FROM inspection i WHERE i.station_id=s.station_id AND i.captured_at>=:start) today_count
                FROM station s LEFT JOIN recipe r ON r.recipe_id=s.recipe_id ORDER BY s.station_id
                """).param("start", start).query((rs,n) -> new StationResponse(rs.getString("station_id"), rs.getString("name"),
                        rs.getString("status"), valueOrDash(rs.getString("product_id")),
                        rs.getObject("version") == null ? "-" : "v" + rs.getInt("version"), rs.getString("camera_status"),
                        rs.getString("ai_status"), rs.getString("mes_status"), rs.getObject("last_seen", OffsetDateTime.class),
                        rs.getLong("today_count"))).list();
    }

    public StationResponse station(String id) {
        return stations().stream().filter(item -> item.id().equals(id)).findFirst()
                .orElseThrow(() -> notFound("스테이션을 찾을 수 없습니다."));
    }

    public List<ModelResponse> models() {
        return jdbc.sql("SELECT * FROM ai_model ORDER BY active DESC,created_at DESC")
                .query((rs,n) -> new ModelResponse(rs.getString("model_id"), rs.getString("name"), rs.getString("version"),
                        rs.getString("framework"), rs.getInt("class_count"), rs.getString("input_size"),
                        rs.getBigDecimal("map50"), rs.getBigDecimal("map95"), rs.getBoolean("active") ? "ACTIVE" : "INACTIVE",
                        rs.getObject("created_at", OffsetDateTime.class).toLocalDate(), rs.getString("note"),
                        modelClasses(rs.getString("model_id")))).list();
    }

    public ModelResponse model(String id) {
        return models().stream().filter(item -> item.id().equals(id)).findFirst()
                .orElseThrow(() -> notFound("모델을 찾을 수 없습니다."));
    }

    @Transactional
    public ModelResponse createModel(ModelRequest request) {
        if (count("ai_model", "model_id", request.id()) > 0) throw conflict("이미 존재하는 모델 ID입니다.");
        jdbc.sql("""
                INSERT INTO ai_model(model_id,name,version,framework,class_count,input_size,map50,map95,active,note)
                VALUES(:id,:name,:version,:framework,:classes,:input,:map50,:map95,false,:note)
                """).param("id", request.id()).param("name", request.name()).param("version", request.version())
                .param("framework", request.framework()).param("classes", request.classes()).param("input", request.input())
                .param("map50", request.map50()).param("map95", request.map95()).param("note", request.note()).update();
        List<String> classes = request.classNames() == null || request.classNames().isEmpty() ? DEFAULT_CLASSES : request.classNames();
        classes.stream().distinct().forEach(name -> jdbc.sql("INSERT INTO ai_model_class(model_id,class_name) VALUES(:id,:name)")
                .param("id", request.id()).param("name", name).update());
        return model(request.id());
    }

    @Transactional
    public ModelResponse activateModel(String id) {
        model(id);
        jdbc.sql("UPDATE ai_model SET active=false WHERE active=true").update();
        jdbc.sql("UPDATE ai_model SET active=true WHERE model_id=:id").param("id", id).update();
        return model(id);
    }

    public DashboardResponse dashboard() {
        LocalDate today = LocalDate.now(KST);
        List<InspectionRow> rows = inspectionRows(today, today, null, null, null);
        long total = rows.size();
        long pass = rows.stream().filter(row -> "PASS".equals(row.verdict())).count();
        long fail = rows.stream().filter(row -> "FAIL".equals(row.verdict())).count();
        List<StationResponse> stationRows = stations();
        DashboardStats stats = new DashboardStats(total, pass, fail, percent(fail, total),
                stationRows.stream().filter(s -> "RUNNING".equals(s.status())).count(),
                stationRows.stream().filter(s -> !"OFFLINE".equals(s.status())).count(), stationRows.size(),
                stationRows.stream().filter(s -> "ERROR".equals(s.status())).count());

        Map<String,List<InspectionRow>> byHour = rows.stream().collect(Collectors.groupingBy(
                row -> String.format("%02d", row.capturedAt().atZoneSameInstant(KST).getHour()), LinkedHashMap::new, Collectors.toList()));
        List<HourlyPoint> hourly = byHour.entrySet().stream().sorted(Map.Entry.comparingByKey())
                .map(entry -> new HourlyPoint(entry.getKey(), entry.getValue().size(),
                percent(entry.getValue().stream().filter(row -> "FAIL".equals(row.verdict())).count(), entry.getValue().size()))).toList();
        return new DashboardResponse(stats, hourly, distribution(rows), recent(rows));
    }

    public DefectAnalyticsResponse defects(LocalDate from, LocalDate to, String productId, String stationId, String modelId) {
        LocalDate end = to == null ? LocalDate.now(KST) : to;
        LocalDate start = from == null ? end.minusDays(6) : from;
        List<InspectionRow> rows = inspectionRows(start, end, blank(productId), blank(stationId), blank(modelId));
        long fail = rows.stream().filter(row -> "FAIL".equals(row.verdict())).count();

        Map<LocalDate,List<InspectionRow>> byDay = rows.stream().collect(Collectors.groupingBy(
                row -> row.capturedAt().atZoneSameInstant(KST).toLocalDate()));
        List<DailyPoint> daily = start.datesUntil(end.plusDays(1)).map(day -> {
            List<InspectionRow> dayRows = byDay.getOrDefault(day, List.of());
            return new DailyPoint(day.format(DateTimeFormatter.ofPattern("MM/dd")), percent(
                    dayRows.stream().filter(row -> "FAIL".equals(row.verdict())).count(), dayRows.size()));
        }).toList();
        double improvement = daily.size() < 2 || daily.get(0).rate() == 0 ? 0
                : round((daily.get(0).rate() - daily.get(daily.size()-1).rate()) / daily.get(0).rate() * 100);
        return new DefectAnalyticsResponse(fail, percent(fail, rows.size()), improvement, distribution(rows), daily,
                rates(rows, true), rates(rows, false), distinctProducts(), distinctStations(), modelIds());
    }

    private List<InspectionRow> inspectionRows(LocalDate from, LocalDate to, String productId, String stationId, String modelId) {
        StringBuilder sql = new StringBuilder("""
                SELECT i.inspection_id,i.product_id,i.station_id,i.model_id,i.verdict,i.captured_at,r.version,
                       (SELECT MIN(pr.defect_type) FROM part_result pr WHERE pr.inspection_id=i.inspection_id AND pr.defect_type IS NOT NULL) defect_type
                FROM inspection i LEFT JOIN recipe r ON r.recipe_id=i.recipe_id
                WHERE i.captured_at>=:fromTime AND i.captured_at<:toTime
                """);
        if (productId != null) sql.append(" AND i.product_id=:productId");
        if (stationId != null) sql.append(" AND i.station_id=:stationId");
        if (modelId != null) sql.append(" AND i.model_id=:modelId");
        sql.append(" ORDER BY i.captured_at DESC");
        JdbcClient.StatementSpec statement = jdbc.sql(sql.toString())
                .param("fromTime", from.atStartOfDay().atOffset(KST)).param("toTime", to.plusDays(1).atStartOfDay().atOffset(KST));
        if (productId != null) statement = statement.param("productId", productId);
        if (stationId != null) statement = statement.param("stationId", stationId);
        if (modelId != null) statement = statement.param("modelId", modelId);
        return statement.query((rs,n) -> new InspectionRow(rs.getString("inspection_id"), rs.getString("product_id"),
                rs.getString("station_id"), rs.getString("model_id"), rs.getString("verdict"),
                rs.getObject("captured_at", OffsetDateTime.class), rs.getObject("version") == null ? null : rs.getInt("version"),
                rs.getString("defect_type"))).list();
    }

    private List<DistributionPoint> distribution(List<InspectionRow> rows) {
        Map<String,Long> counts = rows.stream().filter(row -> row.defectType() != null)
                .collect(Collectors.groupingBy(InspectionRow::defectType, LinkedHashMap::new, Collectors.counting()));
        long total = counts.values().stream().mapToLong(Long::longValue).sum();
        return counts.entrySet().stream().map(entry -> new DistributionPoint(defectLabel(entry.getKey()),
                Math.round(percent(entry.getValue(), total)), entry.getKey())).toList();
    }

    private List<RecentInspection> recent(List<InspectionRow> rows) {
        return rows.stream().filter(row -> "FAIL".equals(row.verdict())).limit(5).map(row -> new RecentInspection(
                row.id(), row.capturedAt().atZoneSameInstant(KST).format(DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss")),
                row.productId(), valueOrDash(row.stationId()), row.version() == null ? "-" : "v" + row.version(),
                row.verdict(), valueOrDash(row.defectType()))).toList();
    }

    private List<RatePoint> rates(List<InspectionRow> rows, boolean product) {
        return rows.stream().collect(Collectors.groupingBy(row -> valueOrDash(product ? row.productId() : row.stationId())))
                .entrySet().stream().map(entry -> new RatePoint(entry.getKey(), percent(
                        entry.getValue().stream().filter(row -> "FAIL".equals(row.verdict())).count(), entry.getValue().size())))
                .sorted((a,b) -> a.name().compareTo(b.name())).toList();
    }

    private List<String> distinctProducts() { return jdbc.sql("SELECT product_id FROM product ORDER BY product_id").query(String.class).list(); }
    private List<String> distinctStations() { return jdbc.sql("SELECT station_id FROM station ORDER BY station_id").query(String.class).list(); }
    private List<String> modelIds() { return jdbc.sql("SELECT model_id FROM ai_model ORDER BY model_id").query(String.class).list(); }
    private List<String> modelClasses(String id) { return jdbc.sql("SELECT class_name FROM ai_model_class WHERE model_id=:id ORDER BY class_name").param("id",id).query(String.class).list(); }
    private int count(String table, String column, String id) { return jdbc.sql("SELECT COUNT(*) FROM " + table + " WHERE " + column + "=:id").param("id",id).query(Integer.class).single(); }
    private static String blank(String value) { return value == null || value.isBlank() ? null : value; }
    private static String valueOrDash(String value) { return value == null || value.isBlank() ? "-" : value; }
    private static double percent(long value, long total) { return total == 0 ? 0 : round(value * 100.0 / total); }
    private static double round(double value) { return BigDecimal.valueOf(value).setScale(2, RoundingMode.HALF_UP).doubleValue(); }
    private static String defectLabel(String key) { return switch (key) {
        case "MISSING" -> "부품 누락"; case "OFFSET" -> "위치 불량"; case "WRONG_PART" -> "오부품";
        case "WIRE_COLOR_SWAP" -> "전선 색상"; case "EXTRA" -> "추가 부품"; default -> key;
    }; }
    private static ResponseStatusException notFound(String message) { return new ResponseStatusException(HttpStatus.NOT_FOUND, message); }
    private static ResponseStatusException conflict(String message) { return new ResponseStatusException(HttpStatus.CONFLICT, message); }
    private record InspectionRow(String id, String productId, String stationId, String modelId,
            String verdict, OffsetDateTime capturedAt, Integer version, String defectType) {}
}
