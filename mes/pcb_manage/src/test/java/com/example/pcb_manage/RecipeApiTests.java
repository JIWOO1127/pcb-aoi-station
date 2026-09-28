package com.example.pcb_manage;

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
class RecipeApiTests {
    @Autowired MockMvc mockMvc;
    @Autowired JdbcClient jdbcClient;
    @Autowired PasswordEncoder passwordEncoder;
    @Autowired JwtTokenService jwtTokenService;

    private static final String BODY = """
            {
              "productId":"NE555-B",
              "version":4,
              "note":"핀홀 기준 변경",
              "parts":[
                {"partId":"U1","expectedClass":"ic_chip","holes":["H6","H7"]},
                {"partId":"R1","expectedClass":"resistance330","holes":["E9","H9"]}
              ]
            }
            """;

    @Test
    void adminCreatesActiveRecipeAndDeactivatesPreviousVersion() throws Exception {
        String token = token("RECIPE-ADMIN", "ADMIN");

        mockMvc.perform(post("/api/recipes")
                        .header("Authorization", "Bearer " + token)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(BODY))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.productId").value("NE555-B"))
                .andExpect(jsonPath("$.version").value(4))
                .andExpect(jsonPath("$.active").value(true))
                .andExpect(jsonPath("$.parts.length()").value(2))
                .andExpect(jsonPath("$.parts[0].partId").value("R1"));

        Integer activeVersion = jdbcClient.sql("SELECT version FROM recipe WHERE product_id='NE555-B' AND active=true")
                .query(Integer.class).single();
        Integer priorActive = jdbcClient.sql("SELECT COUNT(*) FROM recipe WHERE product_id='NE555-B' AND version=3 AND active=true")
                .query(Integer.class).single();
        Integer auditCount = jdbcClient.sql("SELECT COUNT(*) FROM audit_log WHERE action='RECIPE_CREATE'")
                .query(Integer.class).single();

        org.assertj.core.api.Assertions.assertThat(activeVersion).isEqualTo(4);
        org.assertj.core.api.Assertions.assertThat(priorActive).isZero();
        org.assertj.core.api.Assertions.assertThat(auditCount).isEqualTo(1);

        mockMvc.perform(post("/api/recipes")
                        .header("Authorization", "Bearer " + token)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(BODY))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.message").value("이미 존재하는 제품 버전입니다."));
    }

    @Test
    void viewerCannotCreateRecipe() throws Exception {
        mockMvc.perform(post("/api/recipes")
                        .header("Authorization", "Bearer " + token("RECIPE-VIEWER", "VIEWER"))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(BODY))
                .andExpect(status().isForbidden());
    }

    @Test
    void duplicatePartIdIsRejected() throws Exception {
        String duplicateBody = BODY.replace("\"R1\"", "\"U1\"");
        mockMvc.perform(post("/api/recipes")
                        .header("Authorization", "Bearer " + token("RECIPE-ADMIN", "ADMIN"))
                        .contentType(MediaType.APPLICATION_JSON)
                        .content(duplicateBody))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value("부품 ID는 중복될 수 없습니다: U1"));
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
