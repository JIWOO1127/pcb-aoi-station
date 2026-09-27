package com.example.pcb_manage;

import static org.hamcrest.Matchers.matchesPattern;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.test.web.servlet.MockMvc;

import com.example.pcb_manage.auth.JwtTokenService;
import com.example.pcb_manage.auth.WorkerAccount;

@SpringBootTest
@AutoConfigureMockMvc
class PcbManageApplicationTests {
	@Autowired MockMvc mockMvc;
	@Autowired JdbcClient jdbcClient;
	@Autowired PasswordEncoder passwordEncoder;
	@Autowired JwtTokenService jwtTokenService;

	@Test
	void contextLoads() {
	}

	@Test
	void loginReturnsJwtForValidEmployeeIdAndPin() throws Exception {
		insertWorker("A-1042", "홍길동", "1234", true);
		mockMvc.perform(post("/api/auth/login").contentType(MediaType.APPLICATION_JSON)
				.content("{\"employeeId\":\"A-1042\",\"pin\":\"1234\"}"))
				.andExpect(status().isOk())
				.andExpect(jsonPath("$.accessToken", matchesPattern("^[^.]+\\.[^.]+\\.[^.]+$")))
				.andExpect(jsonPath("$.tokenType").value("Bearer"))
				.andExpect(jsonPath("$.expiresIn").value(3600))
				.andExpect(jsonPath("$.user.employeeId").value("A-1042"))
				.andExpect(jsonPath("$.user.role").value("WORKER"));
	}

	@Test
	void loginRejectsWrongPin() throws Exception {
		insertWorker("A-1043", "김관리", "5678", true);
		mockMvc.perform(post("/api/auth/login").contentType(MediaType.APPLICATION_JSON)
				.content("{\"employeeId\":\"A-1043\",\"pin\":\"0000\"}"))
				.andExpect(status().isUnauthorized())
				.andExpect(jsonPath("$.message").value("사번 또는 PIN이 올바르지 않습니다."));
	}

	@Test
	void loginRejectsInactiveWorker() throws Exception {
		insertWorker("A-1044", "박작업", "9012", false);
		mockMvc.perform(post("/api/auth/login").contentType(MediaType.APPLICATION_JSON)
				.content("{\"employeeId\":\"A-1044\",\"pin\":\"9012\"}"))
				.andExpect(status().isUnauthorized());
	}

	@Test
	void issuedJwtCanBeVerifiedAndTamperingIsRejected() {
		String token = jwtTokenService.createToken(new WorkerAccount("ADMIN-1", "관리자", "unused", "ADMIN", true));
		assertEquals("ADMIN-1", jwtTokenService.verify(token).employeeId());
		assertEquals("ADMIN", jwtTokenService.verify(token).role());
		assertThrows(IllegalArgumentException.class, () -> jwtTokenService.verify(token + "tampered"));
	}

	private void insertWorker(String workerId, String name, String pin, boolean active) {
		jdbcClient.sql("""
				INSERT INTO worker (worker_id, name, pin_hash, role, active)
				VALUES (:workerId, :name, :pinHash, 'WORKER', :active)
				""")
				.param("workerId", workerId).param("name", name)
				.param("pinHash", passwordEncoder.encode(pin)).param("active", active).update();
	}
}
