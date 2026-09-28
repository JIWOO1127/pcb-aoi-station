package com.example.pcb_manage;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.multipart;
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
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.mock.web.MockMultipartFile;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.ObjectMapper;

@SpringBootTest
@AutoConfigureMockMvc
@Transactional
class RecipeDraftApiTests {
    @Autowired MockMvc mockMvc;
    @Autowired JdbcClient jdbcClient;
    @Autowired PasswordEncoder passwordEncoder;
    @Autowired JwtTokenService jwtTokenService;
    @Autowired ObjectMapper objectMapper;

    @Test
    void engineerCanAnalyzeWithSampleFallback() throws Exception {
        var image = new MockMultipartFile("image", "board.jpg", MediaType.IMAGE_JPEG_VALUE,
                new byte[] { 1, 2, 3 });
        mockMvc.perform(multipart("/api/recipe-drafts")
                        .file(image).param("productId", "NE555-B")
                        .header("Authorization", "Bearer " + token("DRAFT-ENGINEER", "ENGINEER")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.productId").value("NE555-B"))
                .andExpect(jsonPath("$.parts.length()").value(12))
                .andExpect(jsonPath("$.parts[1].class").value("resistance470"))
                .andExpect(jsonPath("$.overlay.holes[0].x").value(21.0))
                .andExpect(jsonPath("$.warnings[0]").isNotEmpty());
    }

    @Test
    void draftDoesNotReplaceActiveRecipeUntilAdminApproval() throws Exception {
        String draftBody = """
                {"productId":"NE555-B","note":"photo draft","parts":[
                  {"partId":"LED1","class":"led","holes":["D7","E7"],"confidence":0.88,
                   "needsReview":false,"reviewConfirmed":true,"reasons":[]}
                ]}
                """;
        MvcResult saved = mockMvc.perform(post("/api/recipe-drafts")
                        .header("Authorization", "Bearer " + token("DRAFT-ENGINEER-2", "ENGINEER"))
                        .contentType(MediaType.APPLICATION_JSON).content(draftBody))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.version").value(4))
                .andExpect(jsonPath("$.active").value(false))
                .andExpect(jsonPath("$.status").value("DRAFT"))
                .andReturn();

        String recipeId = objectMapper.readTree(saved.getResponse().getContentAsByteArray()).get("recipeId").asText();
        Integer priorActive = jdbcClient.sql("SELECT COUNT(*) FROM recipe WHERE product_id='NE555-B' AND version=3 AND active=true")
                .query(Integer.class).single();
        org.assertj.core.api.Assertions.assertThat(priorActive).isEqualTo(1);

        mockMvc.perform(post("/api/recipes/" + recipeId + "/approve")
                        .header("Authorization", "Bearer " + token("DRAFT-ENGINEER-3", "ENGINEER")))
                .andExpect(status().isForbidden());

        mockMvc.perform(post("/api/recipes/" + recipeId + "/approve")
                        .header("Authorization", "Bearer " + token("DRAFT-ADMIN", "ADMIN")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.active").value(true))
                .andExpect(jsonPath("$.status").value("ACTIVE"))
                .andExpect(jsonPath("$.approvedBy").value("DRAFT-ADMIN"));

        Integer oldActive = jdbcClient.sql("SELECT COUNT(*) FROM recipe WHERE product_id='NE555-B' AND version=3 AND active=true")
                .query(Integer.class).single();
        Integer approvalAudit = jdbcClient.sql("SELECT COUNT(*) FROM audit_log WHERE action='RECIPE_APPROVE' AND target_id=:id")
                .param("id", recipeId).query(Integer.class).single();
        org.assertj.core.api.Assertions.assertThat(oldActive).isZero();
        org.assertj.core.api.Assertions.assertThat(approvalAudit).isEqualTo(1);
    }

    private String token(String workerId, String role) {
        jdbcClient.sql("""
                INSERT INTO worker(worker_id,name,pin_hash,role,admin_role,active)
                VALUES(:id,:name,:pinHash,'ADMIN',:role,true)
                """).param("id", workerId).param("name", workerId)
                .param("pinHash", passwordEncoder.encode("1357")).param("role", role).update();
        return jwtTokenService.createToken(new WorkerAccount(workerId, workerId, "unused", role, true));
    }
}
