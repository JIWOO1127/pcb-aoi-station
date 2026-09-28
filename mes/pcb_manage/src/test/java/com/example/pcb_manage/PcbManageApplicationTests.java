package com.example.pcb_manage;

import static org.hamcrest.Matchers.matchesPattern;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
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
import com.example.pcb_manage.admin.AdminUserController.CreateUserRequest;
import com.example.pcb_manage.admin.AdminUserService;
import com.example.pcb_manage.admin.AuditLogRepository;

@SpringBootTest
@AutoConfigureMockMvc
class PcbManageApplicationTests {
	@Autowired MockMvc mockMvc;
	@Autowired JdbcClient jdbcClient;
	@Autowired PasswordEncoder passwordEncoder;
	@Autowired JwtTokenService jwtTokenService;
	@Autowired AdminUserService adminUserService;
	@Autowired AuditLogRepository auditLogRepository;

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

	@Test
	void adminCanCreateUserAndAuditLogDoesNotContainPassword() {
		var created = adminUserService.create(new CreateUserRequest("WEB-ADMIN", "웹 관리자", "secret-pin", "ADMIN", true), "ROOT");
		assertEquals("ADMIN", created.role());
		var logs = auditLogRepository.list("ROOT", "USER_CREATE", null, null, 0, 20);
		assertEquals(1, logs.size());
		org.junit.jupiter.api.Assertions.assertFalse(logs.get(0).details().contains("secret-pin"));
	}

	@Test
	void adminUserApiSupportsCreateListResetAndRejectsViewer() throws Exception {
		insertManagedUser("ADMIN-API", "관리자", "1234", "ADMIN", true);
		String adminToken = jwtTokenService.createToken(new WorkerAccount("ADMIN-API", "관리자", "unused", "ADMIN", true));

		mockMvc.perform(post("/api/admin/users").header("Authorization", "Bearer " + adminToken)
				.contentType(MediaType.APPLICATION_JSON)
				.content("{\"employeeId\":\"VIEW-API\",\"name\":\"조회자\",\"password\":\"5678\",\"role\":\"VIEWER\",\"active\":true}"))
				.andExpect(status().isOk())
				.andExpect(jsonPath("$.employeeId").value("VIEW-API"));
		mockMvc.perform(get("/api/admin/users").header("Authorization", "Bearer " + adminToken))
				.andExpect(status().isOk());
		mockMvc.perform(post("/api/admin/users/VIEW-API/reset-password").header("Authorization", "Bearer " + adminToken)
				.contentType(MediaType.APPLICATION_JSON).content("{\"password\":\"9999\"}"))
				.andExpect(status().isNoContent());

		String viewerToken = jwtTokenService.createToken(new WorkerAccount("VIEW-API", "조회자", "unused", "VIEWER", true));
		mockMvc.perform(get("/api/admin/users").header("Authorization", "Bearer " + viewerToken))
				.andExpect(status().isForbidden());
	}

	@Test
	void currentAdminCannotDemoteSelfAndDeactivatedTokenIsRejected() throws Exception {
		insertManagedUser("ADMIN-SELF", "자기관리자", "1234", "ADMIN", true);
		String token = jwtTokenService.createToken(new WorkerAccount("ADMIN-SELF", "자기관리자", "unused", "ADMIN", true));
		mockMvc.perform(put("/api/admin/users/ADMIN-SELF").header("Authorization", "Bearer " + token)
				.contentType(MediaType.APPLICATION_JSON)
				.content("{\"name\":\"자기관리자\",\"role\":\"VIEWER\",\"active\":true}"))
				.andExpect(status().isBadRequest());

		jdbcClient.sql("UPDATE worker SET active=false WHERE worker_id='ADMIN-SELF'").update();
		mockMvc.perform(get("/api/auth/me").header("Authorization", "Bearer " + token))
				.andExpect(status().isUnauthorized());
	}

	@Test
	void successfulLoginRecordsLastLoginTime() throws Exception {
		insertWorker("A-LAST", "최근로그인", "1234", true);
		mockMvc.perform(post("/api/auth/login").contentType(MediaType.APPLICATION_JSON)
				.content("{\"employeeId\":\"A-LAST\",\"pin\":\"1234\"}"))
				.andExpect(status().isOk());
		Integer count = jdbcClient.sql("SELECT COUNT(*) FROM worker WHERE worker_id='A-LAST' AND last_login_at IS NOT NULL")
				.query(Integer.class).single();
		assertEquals(1, count);
	}

	private void insertWorker(String workerId, String name, String pin, boolean active) {
		jdbcClient.sql("""
				INSERT INTO worker (worker_id, name, pin_hash, role, active)
				VALUES (:workerId, :name, :pinHash, 'WORKER', :active)
				""")
				.param("workerId", workerId).param("name", name)
				.param("pinHash", passwordEncoder.encode(pin)).param("active", active).update();
	}

	private void insertManagedUser(String workerId, String name, String pin, String role, boolean active) {
		jdbcClient.sql("""
				INSERT INTO worker (worker_id, name, pin_hash, role, admin_role, active)
				VALUES (:workerId, :name, :pinHash, :baseRole, :role, :active)
				""")
				.param("workerId", workerId).param("name", name)
				.param("pinHash", passwordEncoder.encode(pin))
				.param("baseRole", "ADMIN".equals(role) ? "ADMIN" : "WORKER")
				.param("role", role).param("active", active).update();
	}
}
