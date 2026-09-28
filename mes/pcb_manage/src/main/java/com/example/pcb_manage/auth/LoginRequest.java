package com.example.pcb_manage.auth;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record LoginRequest(@NotBlank @Size(max = 50) String employeeId,
        @NotBlank @Size(min = 4, max = 64, message = "PIN은 4~64자여야 합니다.") String pin) {
}
