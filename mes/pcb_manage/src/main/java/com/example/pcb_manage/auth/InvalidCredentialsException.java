package com.example.pcb_manage.auth;

public class InvalidCredentialsException extends RuntimeException {
    public InvalidCredentialsException() {
        super("사번 또는 PIN이 올바르지 않습니다.");
    }
}
