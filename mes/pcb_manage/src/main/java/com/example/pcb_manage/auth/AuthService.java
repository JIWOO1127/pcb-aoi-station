package com.example.pcb_manage.auth;

import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;

@Service
public class AuthService {
    private final WorkerRepository workerRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtTokenService jwtTokenService;

    public AuthService(WorkerRepository workerRepository, PasswordEncoder passwordEncoder,
            JwtTokenService jwtTokenService) {
        this.workerRepository = workerRepository;
        this.passwordEncoder = passwordEncoder;
        this.jwtTokenService = jwtTokenService;
    }

    public LoginResponse login(LoginRequest request) {
        WorkerAccount worker = workerRepository.findById(request.employeeId())
                .filter(WorkerAccount::active)
                .filter(account -> passwordEncoder.matches(request.pin(), account.pinHash()))
                .orElseThrow(InvalidCredentialsException::new);
        return new LoginResponse(jwtTokenService.createToken(worker), "Bearer",
                jwtTokenService.expirationSeconds(),
                new LoginResponse.UserResponse(worker.workerId(), worker.name(), worker.role()));
    }
}
