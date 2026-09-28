package com.example.pcb_manage.admin;

import java.util.List;
import java.time.OffsetDateTime;

import com.example.pcb_manage.auth.JwtAuthenticationFilter.AuthenticatedUser;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/admin/users")
@PreAuthorize("hasRole('ADMIN')")
public class AdminUserController {
    private final AdminUserService service;
    public AdminUserController(AdminUserService service) { this.service = service; }

    @GetMapping
    public List<UserResponse> list() { return service.list(); }

    @PostMapping
    public UserResponse create(@Valid @RequestBody CreateUserRequest request,
            @AuthenticationPrincipal AuthenticatedUser actor) { return service.create(request, actor.employeeId()); }

    @PutMapping("/{employeeId}")
    public UserResponse update(@PathVariable String employeeId, @Valid @RequestBody UpdateUserRequest request,
            @AuthenticationPrincipal AuthenticatedUser actor) { return service.update(employeeId, request, actor.employeeId()); }

    @PostMapping("/{employeeId}/reset-password")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void resetPassword(@PathVariable String employeeId, @Valid @RequestBody ResetPasswordRequest request,
            @AuthenticationPrincipal AuthenticatedUser actor) { service.resetPassword(employeeId, request.password(), actor.employeeId()); }

    public record CreateUserRequest(
            @NotBlank @Pattern(regexp = "[A-Za-z0-9][A-Za-z0-9_-]{2,49}", message = "사번은 영문, 숫자, -, _ 조합으로 3~50자여야 합니다.") String employeeId,
            @NotBlank @Size(max = 100, message = "이름은 100자 이하여야 합니다.") String name,
            @NotBlank @Size(min = 4, max = 64, message = "PIN은 4~64자여야 합니다.") String password,
            @NotBlank @Pattern(regexp = "ADMIN|ENGINEER|VIEWER", message = "올바른 Role을 선택해 주세요.") String role,
            boolean active) {}
    public record UpdateUserRequest(
            @NotBlank @Size(max = 100, message = "이름은 100자 이하여야 합니다.") String name,
            @NotBlank @Pattern(regexp = "ADMIN|ENGINEER|VIEWER", message = "올바른 Role을 선택해 주세요.") String role,
            boolean active) {}
    public record ResetPasswordRequest(
            @NotBlank @Size(min = 4, max = 64, message = "PIN은 4~64자여야 합니다.") String password) {}
    public record UserResponse(String employeeId, String name, String role, boolean active,
            OffsetDateTime lastLoginAt) {}
}
