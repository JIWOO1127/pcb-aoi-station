package com.example.pcb_manage.admin;

import java.util.List;

import com.example.pcb_manage.auth.JwtAuthenticationFilter.AuthenticatedUser;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
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
    public void resetPassword(@PathVariable String employeeId, @Valid @RequestBody ResetPasswordRequest request,
            @AuthenticationPrincipal AuthenticatedUser actor) { service.resetPassword(employeeId, request.password(), actor.employeeId()); }

    public record CreateUserRequest(@NotBlank String employeeId, @NotBlank String name,
            @NotBlank String password, @Pattern(regexp = "ADMIN|ENGINEER|VIEWER") String role) {}
    public record UpdateUserRequest(@NotBlank String name, @Pattern(regexp = "ADMIN|ENGINEER|VIEWER") String role, boolean active) {}
    public record ResetPasswordRequest(@NotBlank String password) {}
    public record UserResponse(String employeeId, String name, String role, boolean active) {}
}
