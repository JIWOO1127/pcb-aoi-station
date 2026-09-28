package com.example.pcb_manage.auth;

import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import com.example.pcb_manage.admin.AuditLogRepository;

@Service
public class AuthService {
    private final WorkerRepository workerRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtTokenService jwtTokenService;
    private final AuditLogRepository auditLogRepository;

    public AuthService(WorkerRepository workerRepository, PasswordEncoder passwordEncoder,
            JwtTokenService jwtTokenService, AuditLogRepository auditLogRepository) {
        this.workerRepository = workerRepository;
        this.passwordEncoder = passwordEncoder;
        this.jwtTokenService = jwtTokenService;
        this.auditLogRepository = auditLogRepository;
    }

    @Transactional
    public LoginResponse login(LoginRequest request) {
        WorkerAccount worker = workerRepository.findById(request.employeeId())
                .filter(WorkerAccount::active)
                .filter(account -> passwordEncoder.matches(request.pin(), account.pinHash()))
                .orElseThrow(InvalidCredentialsException::new);
        workerRepository.recordSuccessfulLogin(worker.workerId());
        auditLogRepository.append(worker.workerId(), "LOGIN", "SESSION", worker.workerId(), "login success");
        return new LoginResponse(jwtTokenService.createToken(worker), "Bearer",
                jwtTokenService.expirationSeconds(),
                new LoginResponse.UserResponse(worker.workerId(), worker.name(), worker.role()));
    }
}
