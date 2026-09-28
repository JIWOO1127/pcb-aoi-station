package com.example.pcb_manage;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.example.pcb_manage.auth.JwtTokenService;
import com.example.pcb_manage.auth.WorkerAccount;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.transaction.annotation.Transactional;

@SpringBootTest
@AutoConfigureMockMvc
@Transactional
class ManagementApiTests {
    @Autowired MockMvc mockMvc;
    @Autowired JdbcClient jdbc;
    @Autowired PasswordEncoder passwordEncoder;
    @Autowired JwtTokenService jwtTokenService;

    @Test
    void seededMasterDataAndDashboardAreAvailable() throws Exception {
        String token = adminToken();
        mockMvc.perform(get("/api/products").header("Authorization", "Bearer " + token))
                .andExpect(status().isOk()).andExpect(jsonPath("$.length()").value(7));
        mockMvc.perform(get("/api/stations").header("Authorization", "Bearer " + token))
                .andExpect(status().isOk()).andExpect(jsonPath("$.length()").value(1))
                .andExpect(jsonPath("$[0].id").value("ST-01"));
        mockMvc.perform(get("/api/models").header("Authorization", "Bearer " + token))
                .andExpect(status().isOk()).andExpect(jsonPath("$.length()").value(3));
        mockMvc.perform(get("/api/dashboard").header("Authorization", "Bearer " + token))
                .andExpect(status().isOk()).andExpect(jsonPath("$.stats.total").isNumber())
                .andExpect(jsonPath("$.recent").isArray());
    }

    @Test
    void adminCanRegisterProductAndModel() throws Exception {
        String token = adminToken();
        mockMvc.perform(post("/api/products").header("Authorization", "Bearer " + token)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"id\":\"PCB-TEST\",\"name\":\"테스트 제품\",\"description\":\"API 테스트\"}"))
                .andExpect(status().isCreated()).andExpect(jsonPath("$.status").value("ACTIVE"));
        mockMvc.perform(post("/api/models").header("Authorization", "Bearer " + token)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"id\":\"MDL-TEST\",\"name\":\"TestNet\",\"version\":\"v1\",\"framework\":\"MMDetection\",\"classes\":8,\"input\":\"640 × 640\",\"map50\":0.9,\"map95\":0.7,\"note\":\"test\",\"classNames\":[]}"))
                .andExpect(status().isCreated()).andExpect(jsonPath("$.id").value("MDL-TEST"))
                .andExpect(jsonPath("$.classNames.length()").value(8));
    }

    private String adminToken() {
        String id = "MANAGEMENT-ADMIN";
        jdbc.sql("INSERT INTO worker(worker_id,name,pin_hash,role,admin_role,active) VALUES(:id,'관리자',:hash,'ADMIN','ADMIN',true)")
                .param("id", id).param("hash", passwordEncoder.encode("1357")).update();
        return jwtTokenService.createToken(new WorkerAccount(id, "관리자", "unused", "ADMIN", true));
    }
}
