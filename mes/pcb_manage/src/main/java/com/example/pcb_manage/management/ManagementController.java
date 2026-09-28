package com.example.pcb_manage.management;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.List;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.PositiveOrZero;
import jakarta.validation.constraints.Size;
import org.springframework.format.annotation.DateTimeFormat;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api")
public class ManagementController {
    private final ManagementService service;

    public ManagementController(ManagementService service) { this.service = service; }

    @GetMapping("/products")
    public List<ProductResponse> products() { return service.products(); }

    @GetMapping("/products/{productId}")
    public ProductResponse product(@PathVariable String productId) { return service.product(productId); }

    @PostMapping("/products")
    @ResponseStatus(HttpStatus.CREATED)
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public ProductResponse createProduct(@Valid @RequestBody ProductRequest request) {
        return service.createProduct(request);
    }

    @PutMapping("/products/{productId}")
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public ProductResponse updateProduct(@PathVariable String productId, @Valid @RequestBody ProductUpdateRequest request) {
        return service.updateProduct(productId, request);
    }

    @GetMapping("/stations")
    public List<StationResponse> stations() { return service.stations(); }

    @GetMapping("/stations/{stationId}")
    public StationResponse station(@PathVariable String stationId) { return service.station(stationId); }

    @GetMapping("/models")
    public List<ModelResponse> models() { return service.models(); }

    @GetMapping("/models/{modelId}")
    public ModelResponse model(@PathVariable String modelId) { return service.model(modelId); }

    @PostMapping("/models")
    @ResponseStatus(HttpStatus.CREATED)
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public ModelResponse createModel(@Valid @RequestBody ModelRequest request) { return service.createModel(request); }

    @PostMapping("/models/{modelId}/activate")
    @PreAuthorize("hasAnyRole('ADMIN','ENGINEER')")
    public ModelResponse activateModel(@PathVariable String modelId) { return service.activateModel(modelId); }

    @GetMapping("/dashboard")
    public DashboardResponse dashboard() { return service.dashboard(); }

    @GetMapping("/analytics/defects")
    public DefectAnalyticsResponse defects(
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate from,
            @RequestParam(required = false) @DateTimeFormat(iso = DateTimeFormat.ISO.DATE) LocalDate to,
            @RequestParam(required = false) String productId,
            @RequestParam(required = false) String stationId,
            @RequestParam(required = false) String modelId) {
        return service.defects(from, to, productId, stationId, modelId);
    }

    public record ProductRequest(@NotBlank @Size(max=50) String id, @NotBlank @Size(max=120) String name,
            @Size(max=500) String description) {}
    public record ProductUpdateRequest(@NotBlank @Size(max=120) String name, @Size(max=500) String description,
            boolean active) {}
    public record ProductResponse(String id, String name, String description, String recipe,
            OffsetDateTime createdAt, String status) {}
    public record StationResponse(String id, String name, String status, String product, String recipe,
            String camera, String ai, String mes, OffsetDateTime lastSeen, long today) {}
    public record ModelRequest(@NotBlank @Size(max=50) String id, @NotBlank @Size(max=100) String name,
            @NotBlank @Size(max=30) String version, @NotBlank @Size(max=50) String framework,
            @PositiveOrZero int classes, @NotBlank @Size(max=30) String input,
            BigDecimal map50, BigDecimal map95, @Size(max=500) String note, List<String> classNames) {}
    public record ModelResponse(String id, String name, String version, String framework, int classes,
            String input, BigDecimal map50, BigDecimal map95, String status, LocalDate createdAt,
            String note, List<String> classNames) {}
    public record DashboardStats(long total, long pass, long fail, double defectRate,
            long runningStations, long connectedStations, long totalStations, long errorStations) {}
    public record HourlyPoint(String time, long inspections, double defectRate) {}
    public record DistributionPoint(String name, long value, String key) {}
    public record RecentInspection(String id, String time, String product, String station,
            String recipe, String result, String defect) {}
    public record DashboardResponse(DashboardStats stats, List<HourlyPoint> hourly,
            List<DistributionPoint> defects, List<RecentInspection> recent) {}
    public record RatePoint(String name, double rate) {}
    public record DailyPoint(String date, double rate) {}
    public record DefectAnalyticsResponse(long failCount, double defectRate, double qualityImprovement,
            List<DistributionPoint> defectDistribution, List<DailyPoint> dailyDefects,
            List<RatePoint> productDefects, List<RatePoint> stationDefects,
            List<String> products, List<String> stations, List<String> models) {}
}
