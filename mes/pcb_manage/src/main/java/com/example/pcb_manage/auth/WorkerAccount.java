package com.example.pcb_manage.auth;

public record WorkerAccount(String workerId, String name, String pinHash, String role, boolean active) {
}
