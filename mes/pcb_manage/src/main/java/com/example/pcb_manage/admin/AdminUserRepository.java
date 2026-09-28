package com.example.pcb_manage.admin;

import java.util.List;
import java.time.OffsetDateTime;

import com.example.pcb_manage.admin.AdminUserController.CreateUserRequest;
import com.example.pcb_manage.admin.AdminUserController.UpdateUserRequest;
import com.example.pcb_manage.admin.AdminUserController.UserResponse;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

@Repository
public class AdminUserRepository {
    private final JdbcClient jdbc;
    public AdminUserRepository(JdbcClient jdbc) { this.jdbc = jdbc; }
    private final org.springframework.jdbc.core.RowMapper<UserResponse> mapper = (rs, n) ->
            new UserResponse(rs.getString("worker_id"), rs.getString("name"), rs.getString("admin_role"),
                    rs.getBoolean("active"), rs.getObject("last_login_at", OffsetDateTime.class));

    public List<UserResponse> list() { return jdbc.sql("SELECT worker_id, name, admin_role, active, last_login_at FROM worker WHERE admin_role IS NOT NULL ORDER BY worker_id").query(mapper).list(); }
    public UserResponse get(String id) { return jdbc.sql("SELECT worker_id, name, admin_role, active, last_login_at FROM worker WHERE worker_id=:id AND admin_role IS NOT NULL").param("id", id).query(mapper).optional().orElse(null); }
    public boolean exists(String id) { return jdbc.sql("SELECT COUNT(*) FROM worker WHERE worker_id=:id").param("id", id).query(Integer.class).single() > 0; }
    public long countActiveAdmins() { return jdbc.sql("SELECT COUNT(*) FROM worker WHERE admin_role='ADMIN' AND active=true").query(Long.class).single(); }
    public void create(CreateUserRequest r, String hash) { jdbc.sql("INSERT INTO worker(worker_id,name,pin_hash,role,admin_role,active) VALUES(:id,:name,:hash,:baseRole,:role,:active)").param("id",r.employeeId()).param("name",r.name()).param("hash",hash).param("baseRole", "ADMIN".equals(r.role()) ? "ADMIN" : "WORKER").param("role",r.role()).param("active",r.active()).update(); }
    public void update(String id, UpdateUserRequest r) { jdbc.sql("UPDATE worker SET name=:name,role=:baseRole,admin_role=:role,active=:active,updated_at=CURRENT_TIMESTAMP WHERE worker_id=:id").param("id",id).param("name",r.name()).param("baseRole", "ADMIN".equals(r.role()) ? "ADMIN" : "WORKER").param("role",r.role()).param("active",r.active()).update(); }
    public void resetPassword(String id, String hash) { jdbc.sql("UPDATE worker SET pin_hash=:hash,updated_at=CURRENT_TIMESTAMP WHERE worker_id=:id").param("id",id).param("hash",hash).update(); }
}
