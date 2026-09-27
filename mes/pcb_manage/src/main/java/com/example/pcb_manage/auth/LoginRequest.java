package com.example.pcb_manage.auth;

import jakarta.validation.constraints.NotBlank;

public record LoginRequest(@NotBlank String employeeId, @NotBlank String pin) {
}
