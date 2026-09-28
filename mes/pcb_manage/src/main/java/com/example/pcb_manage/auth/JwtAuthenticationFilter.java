package com.example.pcb_manage.auth;

import java.io.IOException;
import java.util.List;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.http.HttpHeaders;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

@Component
public class JwtAuthenticationFilter extends OncePerRequestFilter {
    private final JwtTokenService jwtTokenService;
    private final WorkerRepository workerRepository;

    public JwtAuthenticationFilter(JwtTokenService jwtTokenService, WorkerRepository workerRepository) {
        this.jwtTokenService = jwtTokenService;
        this.workerRepository = workerRepository;
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response,
            FilterChain filterChain) throws ServletException, IOException {
        String authorization = request.getHeader(HttpHeaders.AUTHORIZATION);
        if (authorization != null && authorization.startsWith("Bearer ")) {
            try {
                JwtTokenService.TokenClaims claims = jwtTokenService.verify(authorization.substring(7));
                WorkerAccount worker = workerRepository.findById(claims.employeeId())
                        .filter(WorkerAccount::active)
                        .filter(account -> account.role().equals(claims.role()))
                        .orElseThrow(() -> new IllegalArgumentException("계정 상태 또는 권한이 변경되었습니다."));
                var principal = new AuthenticatedUser(worker.workerId(), worker.name(), worker.role());
                var authentication = new UsernamePasswordAuthenticationToken(principal, null,
                        List.of(new SimpleGrantedAuthority("ROLE_" + worker.role())));
                SecurityContextHolder.getContext().setAuthentication(authentication);
            } catch (IllegalArgumentException ignored) {
                SecurityContextHolder.clearContext();
            }
        }
        filterChain.doFilter(request, response);
    }

    public record AuthenticatedUser(String employeeId, String name, String role) {
    }
}
