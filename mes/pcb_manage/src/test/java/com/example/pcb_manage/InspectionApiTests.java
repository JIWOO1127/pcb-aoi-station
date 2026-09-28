package com.example.pcb_manage;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.UUID;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.transaction.annotation.Transactional;

import com.example.pcb_manage.auth.JwtTokenService;
import com.example.pcb_manage.auth.WorkerAccount;

@SpringBootTest
@AutoConfigureMockMvc
@Transactional
class InspectionApiTests {
    private static final UUID RECIPE_ID = UUID.fromString("7bafaa9d-4d9f-4d2e-bc04-0db47d401003");

    @Autowired MockMvc mockMvc;
    @Autowired JdbcClient jdbcClient;
    @Autowired PasswordEncoder passwordEncoder;
    @Autowired JwtTokenService jwtTokenService;

    @Test
    void recipeApiReturnsProductVersionNoteAndParts() throws Exception {
        String token = adminToken();

        mockMvc.perform(get("/api/recipes/{id}", RECIPE_ID)
                        .header("Authorization", "Bearer " + token))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.recipeId").value(RECIPE_ID.toString()))
                .andExpect(jsonPath("$.productId").value("NE555-B"))
                .andExpect(jsonPath("$.version").value(3))
                .andExpect(jsonPath("$.note").isNotEmpty())
                .andExpect(jsonPath("$.parts.length()").value(12))
                .andExpect(jsonPath("$.parts[0].partId").value("C1"))
                .andExpect(jsonPath("$.parts[0].holes[0]").value("Q10"));
    }

    @Test
    void stationCanCreateInspectionAndAdminCanReadIt() throws Exception {
        UUID inspectionId = UUID.randomUUID();
        String body = """
                {
                  "inspectionId":"%s",
                  "recipeId":"%s",
                  "stationId":"ST-01",
                  "productId":"NE555-B",
                  "workerId":null,
                  "verdict":"PASS",
                  "defectCount":0,
                  "errorCode":null,
                  "latencyMs":321,
                  "capturedAt":"2026-09-28T14:03:22.418+09:00",
                  "parts":[{
                    "partId":"R1",
                    "expectedClass":"resistance330",
                    "detectedClass":"resistance330",
                    "defectType":null,
                    "expectedHoles":["E9","H9"],
                    "coveredHoles":["E9","H9"]
                  }]
                }
                """.formatted(inspectionId, RECIPE_ID);

        mockMvc.perform(post("/api/inspections")
                        .header("X-Station-Key", "test-station-key")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.inspectionId").value(inspectionId.toString()));

        mockMvc.perform(post("/api/inspections")
                        .header("X-Station-Key", "test-station-key")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(body))
                .andExpect(status().isConflict());

        mockMvc.perform(get("/api/inspections/{id}", inspectionId)
                        .header("Authorization", "Bearer " + adminToken()))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.recipeId").value(RECIPE_ID.toString()))
                .andExpect(jsonPath("$.latencyMs").value(321))
                .andExpect(jsonPath("$.parts[0].expectedHoles[0]").value("E9"));
    }

    @Test
    void inspectionPostRequiresStationKey() throws Exception {
        mockMvc.perform(post("/api/inspections")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isUnauthorized());
    }

    private String adminToken() {
        String workerId = "INSPECTION-ADMIN";
        Integer count = jdbcClient.sql("SELECT COUNT(*) FROM worker WHERE worker_id=:id")
                .param("id", workerId).query(Integer.class).single();
        if (count == 0) {
            jdbcClient.sql("""
                    INSERT INTO worker(worker_id,name,pin_hash,role,admin_role,active)
                    VALUES(:id,'검사 관리자',:pinHash,'ADMIN','ADMIN',true)
                    """).param("id", workerId).param("pinHash", passwordEncoder.encode("1357")).update();
        }
        return jwtTokenService.createToken(new WorkerAccount(workerId, "검사 관리자", "unused", "ADMIN", true));
    }
}
