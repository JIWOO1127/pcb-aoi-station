package com.example.pcb_manage.auth;

import java.util.Optional;

import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

@Repository
public class WorkerRepository {
    private final JdbcClient jdbcClient;

    public WorkerRepository(JdbcClient jdbcClient) {
        this.jdbcClient = jdbcClient;
    }

    public Optional<WorkerAccount> findById(String workerId) {
        return jdbcClient.sql("""
                        SELECT worker_id, name, pin_hash, COALESCE(admin_role, role) AS role, active
                        FROM worker WHERE worker_id = :workerId
                        """)
                .param("workerId", workerId)
                .query((rs, rowNum) -> new WorkerAccount(
                        rs.getString("worker_id"), rs.getString("name"),
                        rs.getString("pin_hash"), rs.getString("role"), rs.getBoolean("active")))
                .optional();
    }
}
