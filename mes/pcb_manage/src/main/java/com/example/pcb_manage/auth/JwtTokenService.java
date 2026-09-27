package com.example.pcb_manage.auth;

import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Base64;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

@Service
public class JwtTokenService {
    private static final Base64.Encoder BASE64_URL = Base64.getUrlEncoder().withoutPadding();
    private final byte[] secret;
    private final long expirationSeconds;

    public JwtTokenService(@Value("${app.security.jwt-secret}") String secret,
            @Value("${app.security.jwt-expiration-seconds}") long expirationSeconds) {
        if (secret.getBytes(StandardCharsets.UTF_8).length < 32) {
            throw new IllegalArgumentException("JWT_SECRET은 32바이트 이상이어야 합니다.");
        }
        this.secret = secret.getBytes(StandardCharsets.UTF_8);
        this.expirationSeconds = expirationSeconds;
    }

    public String createToken(WorkerAccount worker) {
        long issuedAt = Instant.now().getEpochSecond();
        String header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        String payload = "{\"sub\":\"%s\",\"name\":\"%s\",\"role\":\"%s\",\"iat\":%d,\"exp\":%d}"
                .formatted(escape(worker.workerId()), escape(worker.name()), escape(worker.role()),
                        issuedAt, issuedAt + expirationSeconds);
        String content = encodeJson(header) + "." + encodeJson(payload);
        return content + "." + sign(content);
    }

    public long expirationSeconds() {
        return expirationSeconds;
    }

    public TokenClaims verify(String token) {
        try {
            String[] parts = token.split("\\.");
            if (parts.length != 3) {
                throw new IllegalArgumentException("잘못된 JWT 형식입니다.");
            }
            String content = parts[0] + "." + parts[1];
            byte[] expected = Base64.getUrlDecoder().decode(sign(content));
            byte[] actual = Base64.getUrlDecoder().decode(parts[2]);
            if (!java.security.MessageDigest.isEqual(expected, actual)) {
                throw new IllegalArgumentException("JWT 서명이 올바르지 않습니다.");
            }
            String payload = new String(Base64.getUrlDecoder().decode(parts[1]), StandardCharsets.UTF_8);
            TokenClaims claims = new TokenClaims(readString(payload, "sub"), readString(payload, "name"),
                    readString(payload, "role"), readLong(payload, "exp"));
            if (claims.expiresAt() <= Instant.now().getEpochSecond()) {
                throw new IllegalArgumentException("JWT가 만료되었습니다.");
            }
            return claims;
        } catch (IllegalArgumentException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new IllegalArgumentException("JWT를 확인할 수 없습니다.", exception);
        }
    }

    private String readString(String json, String key) {
        Matcher matcher = Pattern.compile("\\\"" + key + "\\\":\\\"((?:\\\\.|[^\\\"])*)\\\"").matcher(json);
        if (!matcher.find()) throw new IllegalArgumentException("JWT 필드가 없습니다: " + key);
        return matcher.group(1).replace("\\\"", "\"").replace("\\\\", "\\");
    }

    private long readLong(String json, String key) {
        Matcher matcher = Pattern.compile("\\\"" + key + "\\\":(\\d+)").matcher(json);
        if (!matcher.find()) throw new IllegalArgumentException("JWT 필드가 없습니다: " + key);
        return Long.parseLong(matcher.group(1));
    }

    public record TokenClaims(String employeeId, String name, String role, long expiresAt) {
    }

    private String encodeJson(String value) {
        return BASE64_URL.encodeToString(value.getBytes(StandardCharsets.UTF_8));
    }

    private String escape(String value) {
        return value.replace("\\", "\\\\").replace("\"", "\\\"")
                .replace("\n", "\\n").replace("\r", "\\r");
    }

    private String sign(String content) {
        try {
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(secret, "HmacSHA256"));
            return BASE64_URL.encodeToString(mac.doFinal(content.getBytes(StandardCharsets.UTF_8)));
        } catch (Exception exception) {
            throw new IllegalStateException("JWT 서명에 실패했습니다.", exception);
        }
    }
}
