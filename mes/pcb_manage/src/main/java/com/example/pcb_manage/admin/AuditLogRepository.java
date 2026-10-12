package com.example.pcb_manage.admin;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.UUID;

import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

@Repository
public class AuditLogRepository {
    private final JdbcClient jdbc;
    public AuditLogRepository(JdbcClient jdbc) { this.jdbc = jdbc; }

    public void append(String actorId, String action, String targetType, String targetId, String details) {
        jdbc.sql("INSERT INTO audit_log(audit_id,actor_id,action,target_type,target_id,details) VALUES(:id,:actor,:action,:type,:target,:details)")
                .param("id", UUID.randomUUID()).param("actor", actorId).param("action", action)
                .param("type", targetType).param("target", targetId).param("details", details).update();
    }

    public List<AuditLogResponse> list(String actorId, String action, OffsetDateTime from, OffsetDateTime to,
            int page, int size) {
        StringBuilder sql = new StringBuilder("""
            SELECT audit_id, actor_id, action, target_type, target_id, details, occurred_at
            FROM audit_log
            WHERE 1=1
            """);
        if (actorId != null && !actorId.isBlank()) sql.append(" AND actor_id=:actor");
        if (action != null && !action.isBlank()) sql.append(" AND action=:action");
        if (from != null) sql.append(" AND occurred_at>=:fromTime");
        if (to != null) sql.append(" AND occurred_at<=:toTime");
        sql.append(" ORDER BY occurred_at DESC LIMIT :size OFFSET :offset");

        JdbcClient.StatementSpec statement = jdbc.sql(sql.toString());
        if (actorId != null && !actorId.isBlank()) statement = statement.param("actor", actorId);
        if (action != null && !action.isBlank()) statement = statement.param("action", action);
        if (from != null) statement = statement.param("fromTime", from);
        if (to != null) statement = statement.param("toTime", to);
        return statement.param("size", size).param("offset", page * size)
                .query((rs, n) -> new AuditLogResponse(rs.getObject("audit_id", UUID.class),
                        rs.getString("actor_id"), rs.getString("action"), rs.getString("target_type"),
                        rs.getString("target_id"), rs.getString("details"),
                        rs.getObject("occurred_at", OffsetDateTime.class))).list();
    }

    public record AuditLogResponse(UUID auditId, String actorId, String action, String targetType,
            String targetId, String details, OffsetDateTime occurredAt) {}
}
