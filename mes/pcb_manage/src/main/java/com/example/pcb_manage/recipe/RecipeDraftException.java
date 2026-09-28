package com.example.pcb_manage.recipe;

import org.springframework.http.HttpStatus;

public class RecipeDraftException extends RuntimeException {
    private final HttpStatus status;
    private final String code;

    public RecipeDraftException(HttpStatus status, String code, String message) {
        super(message);
        this.status = status;
        this.code = code;
    }

    public HttpStatus status() { return status; }
    public String code() { return code; }
}
