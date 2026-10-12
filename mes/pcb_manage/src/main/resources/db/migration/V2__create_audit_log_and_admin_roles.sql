ALTER TABLE worker ADD COLUMN admin_role VARCHAR(20)
    CHECK (admin_role IN ('ADMIN', 'ENGINEER', 'VIEWER'));
UPDATE worker SET admin_role = 'ADMIN' WHERE role = 'ADMIN';

CREATE TABLE audit_log (
    audit_id UUID PRIMARY KEY,
    actor_id VARCHAR(50),
    action VARCHAR(30) NOT NULL,
    target_type VARCHAR(30) NOT NULL,
    target_id VARCHAR(100),
    details VARCHAR(1000),
    occurred_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_log_occurred_at ON audit_log(occurred_at);
CREATE INDEX idx_audit_log_actor_action ON audit_log(actor_id, action);
