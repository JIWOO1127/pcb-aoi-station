package com.example.pcb_manage.inspection;

import static com.example.pcb_manage.inspection.InspectionController.*;

import java.util.List;
import java.util.UUID;

import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

@Service
public class InspectionService {
    private final InspectionRepository repository;

    public InspectionService(InspectionRepository repository) {
        this.repository = repository;
    }

    @Transactional
    public void create(CreateInspectionRequest request) {
        if (repository.inspectionExists(request.inspectionId())) conflict("이미 저장된 검사 기록입니다.");
        if (!repository.productExists(request.productId())) badRequest("존재하지 않거나 비활성인 제품입니다.");
        UUID recipeId = resolveRecipeId(request);
        if (request.workerId() != null && !repository.workerExists(request.workerId())) {
            badRequest("존재하지 않거나 비활성인 작업자입니다.");
        }
        if ("INVALID".equals(request.verdict()) && !request.parts().isEmpty()) {
            badRequest("INVALID 검사의 parts는 비어 있어야 합니다.");
        }
        for (PartRequest part : request.parts()) {
            boolean extra = "EXTRA".equals(part.defectType());
            if (!extra && (part.expectedClass() == null || part.expectedClass().isBlank())) {
                badRequest("EXTRA가 아닌 부품의 expectedClass는 필수입니다.");
            }
            if (extra && !part.expectedHoles().isEmpty()) {
                badRequest("EXTRA 부품의 expectedHoles는 비어 있어야 합니다.");
            }
        }
        repository.insert(request, recipeId);
        request.parts().forEach(part -> repository.insertPart(request.inspectionId(), part));
    }

    private UUID resolveRecipeId(CreateInspectionRequest request) {
        if (request.recipeId() == null) {
            return repository.activeRecipeId(request.productId())
                    .orElseThrow(() -> new ResponseStatusException(HttpStatus.BAD_REQUEST,
                            "제품에 활성 레시피가 없습니다."));
        }
        if (!repository.recipeMatches(request.recipeId(), request.productId())) {
            badRequest("제품과 레시피가 일치하지 않습니다.");
        }
        return request.recipeId();
    }

    public List<InspectionSummary> list(String verdict, String productId, String stationId) {
        return repository.list(verdict, productId, stationId);
    }

    public InspectionDetail get(UUID id) {
        InspectionSummary summary = repository.getSummary(id);
        if (summary == null) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "검사 기록을 찾을 수 없습니다.");
        }
        return new InspectionDetail(summary.inspectionId(), summary.recipeId(), summary.recipeVersion(),
                summary.stationId(), summary.productId(), summary.workerId(), summary.verdict(),
                summary.defectCount(), summary.errorCode(), summary.latencyMs(), summary.capturedAt(),
                repository.parts(id), repository.confirmations(id));
    }

    @Transactional
    public void confirm(UUID inspectionId, ConfirmationRequest request) {
        if (!repository.inspectionExists(inspectionId)) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "검사 기록을 찾을 수 없습니다.");
        }
        if (repository.confirmationExists(request.confirmationId())) conflict("이미 저장된 확인 기록입니다.");
        if (!repository.workerExists(request.workerId())) badRequest("존재하지 않거나 비활성인 작업자입니다.");
        repository.insertConfirmation(inspectionId, request);
    }

    private static void badRequest(String message) {
        throw new ResponseStatusException(HttpStatus.BAD_REQUEST, message);
    }

    private static void conflict(String message) {
        throw new ResponseStatusException(HttpStatus.CONFLICT, message);
    }
}
