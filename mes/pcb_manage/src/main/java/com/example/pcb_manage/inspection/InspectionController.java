package com.example.pcb_manage.inspection;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/inspections")
public class InspectionController {
    private final InspectionService service;
    public InspectionController(InspectionService service) { this.service = service; }

    @PostMapping
    public ResponseEntity<InspectionCreatedResponse> create(@Valid @RequestBody CreateInspectionRequest request) {
        service.create(request);
        return ResponseEntity.status(HttpStatus.CREATED).body(new InspectionCreatedResponse(request.inspectionId()));
    }

    @GetMapping
    public List<InspectionSummary> list(@RequestParam(required = false) String verdict,
            @RequestParam(required = false) String productId,
            @RequestParam(required = false) String stationId) {
        return service.list(verdict, productId, stationId);
    }

    @GetMapping("/{inspectionId}")
    public InspectionDetail get(@PathVariable UUID inspectionId) { return service.get(inspectionId); }

    @PostMapping("/{inspectionId}/confirmations")
    public ResponseEntity<ConfirmationCreatedResponse> confirm(@PathVariable UUID inspectionId,
            @Valid @RequestBody ConfirmationRequest request) {
        service.confirm(inspectionId, request);
        return ResponseEntity.status(HttpStatus.CREATED).body(new ConfirmationCreatedResponse(request.confirmationId()));
    }

    public record CreateInspectionRequest(
            @NotNull UUID inspectionId,
            UUID recipeId,
            @NotBlank @Size(max = 50) String stationId,
            @NotBlank @Size(max = 50) String productId,
            @Size(max = 50) String workerId,
            @NotBlank @Pattern(regexp = "PASS|FAIL|INVALID") String verdict,
            @Min(0) int defectCount,
            @Size(max = 50) String errorCode,
            @Min(0) int latencyMs,
            @NotNull OffsetDateTime capturedAt,
            @NotNull List<@Valid PartRequest> parts) {}

    public record PartRequest(
            @NotBlank @Size(max = 50) String partId,
            @Size(max = 100) String expectedClass,
            @Size(max = 100) String detectedClass,
            @Pattern(regexp = "MISSING|WRONG_PART|WIRE_COLOR_SWAP|OFFSET|EXTRA") String defectType,
            @NotNull List<@Pattern(regexp = "[A-U](?:[0-9]|1[0-3])") String> expectedHoles,
            @NotNull List<@Pattern(regexp = "[A-U](?:[0-9]|1[0-3])") String> coveredHoles) {}

    public record ConfirmationRequest(@NotNull UUID confirmationId, @NotBlank String workerId,
            @NotBlank @Size(max = 20) String decision, @Size(max = 50) String reasonCode,
            @NotNull OffsetDateTime confirmedAt) {}
    public record InspectionCreatedResponse(UUID inspectionId) {}
    public record ConfirmationCreatedResponse(UUID confirmationId) {}
    public record InspectionSummary(UUID inspectionId, UUID recipeId, int recipeVersion,
            String stationId, String productId, String workerId, String verdict, int defectCount,
            String errorCode, int latencyMs, OffsetDateTime capturedAt) {}
    public record PartResponse(String partId, String expectedClass, String detectedClass,
            String defectType, List<String> expectedHoles, List<String> coveredHoles) {}
    public record ConfirmationResponse(UUID confirmationId, String workerId, String decision,
            String reasonCode, OffsetDateTime confirmedAt) {}
    public record InspectionDetail(UUID inspectionId, UUID recipeId, int recipeVersion,
            String stationId, String productId, String workerId, String verdict, int defectCount,
            String errorCode, int latencyMs, OffsetDateTime capturedAt,
            List<PartResponse> parts, List<ConfirmationResponse> confirmations) {}
}
