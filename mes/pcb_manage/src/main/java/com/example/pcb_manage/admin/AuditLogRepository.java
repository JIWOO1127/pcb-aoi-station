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
        return jdbc.sql("""
                SELECT audit_id, actor_id, action, target_type, target_id, details, occurred_at
                FROM audit_log
                WHERE (:actor IS NULL OR actor_id = :actor)
                  AND (:action IS NULL OR action = :action)
                  AND (:fromTime IS NULL OR occurred_at >= :fromTime)
                  AND (:toTime IS NULL OR occurred_at <= :toTime)
                ORDER BY occurred_at DESC LIMIT :size OFFSET :offset
                """).param("actor", actorId).param("action", action).param("fromTime", from).param("toTime", to)
                .param("size", size).param("offset", page * size)
                .query((rs, n) -> new AuditLogResponse(rs.getObject("audit_id", UUID.class),
                        rs.getString("actor_id"), rs.getString("action"), rs.getString("target_type"),
                        rs.getString("target_id"), rs.getString("details"), rs.getObject("occurred_at", OffsetDateTime.class))).list();
    }

    public record AuditLogResponse(UUID auditId, String actorId, String action, String targetType,
            String targetId, String details, OffsetDateTime occurredAt) {}
}
