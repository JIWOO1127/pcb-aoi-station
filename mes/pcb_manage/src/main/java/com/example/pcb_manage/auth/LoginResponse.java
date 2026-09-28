package com.example.pcb_manage.auth;

public record LoginResponse(String accessToken, String tokenType, long expiresIn, UserResponse user) {
    public record UserResponse(String employeeId, String name, String role) {
    }
}
