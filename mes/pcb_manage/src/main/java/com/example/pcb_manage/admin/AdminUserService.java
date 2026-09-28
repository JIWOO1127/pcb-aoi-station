package com.example.pcb_manage.admin;

import java.util.List;

import com.example.pcb_manage.admin.AdminUserController.CreateUserRequest;
import com.example.pcb_manage.admin.AdminUserController.UpdateUserRequest;
import com.example.pcb_manage.admin.AdminUserController.UserResponse;
import org.springframework.http.HttpStatus;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

@Service
public class AdminUserService {
    private final AdminUserRepository repository;
    private final AuditLogRepository auditLogRepository;
    private final PasswordEncoder passwordEncoder;

    public AdminUserService(AdminUserRepository repository, AuditLogRepository auditLogRepository,
            PasswordEncoder passwordEncoder) {
        this.repository = repository; this.auditLogRepository = auditLogRepository; this.passwordEncoder = passwordEncoder;
    }

    public List<UserResponse> list() { return repository.list(); }

    @Transactional
    public UserResponse create(CreateUserRequest request, String actorId) {
        if (repository.exists(request.employeeId())) throw new ResponseStatusException(HttpStatus.CONFLICT, "이미 존재하는 사번입니다.");
        repository.create(request, passwordEncoder.encode(request.password()));
        auditLogRepository.append(actorId, "USER_CREATE", "USER", request.employeeId(),
                "role=" + request.role() + ", active=" + request.active());
        return repository.get(request.employeeId());
    }

    @Transactional
    public UserResponse update(String employeeId, UpdateUserRequest request, String actorId) {
        UserResponse before = getManagedUser(employeeId);
        if (employeeId.equals(actorId) && (!request.active() || !"ADMIN".equals(request.role()))) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "현재 로그인한 계정은 비활성화하거나 관리자 권한을 해제할 수 없습니다.");
        }
        boolean removesActiveAdmin = before.active() && "ADMIN".equals(before.role())
                && (!request.active() || !"ADMIN".equals(request.role()));
        if (removesActiveAdmin && repository.countActiveAdmins() <= 1) {
            throw new ResponseStatusException(HttpStatus.CONFLICT, "마지막 활성 관리자 계정은 비활성화하거나 권한을 변경할 수 없습니다.");
        }
        repository.update(employeeId, request);
        auditLogRepository.append(actorId, "USER_UPDATE", "USER", employeeId,
                "name=" + before.name() + " -> " + request.name()
                        + ", role=" + before.role() + " -> " + request.role()
                        + ", active=" + before.active() + " -> " + request.active());
        return repository.get(employeeId);
    }

    @Transactional
    public void resetPassword(String employeeId, String password, String actorId) {
        getManagedUser(employeeId); repository.resetPassword(employeeId, passwordEncoder.encode(password));
        auditLogRepository.append(actorId, "USER_PASSWORD_RESET", "USER", employeeId, "password reset");
    }

    private UserResponse getManagedUser(String employeeId) {
        UserResponse user = repository.get(employeeId);
        if (user == null) throw new ResponseStatusException(HttpStatus.NOT_FOUND, "사용자를 찾을 수 없습니다.");
        return user;
    }
}
