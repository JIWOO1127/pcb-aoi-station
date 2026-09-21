# MES 서버 (Java)

- 담당: 
- 기술: Java, Spring Boot, PostgreSQL
- 포트: 8080 (DB 5432)

이 폴더 안에 Spring Boot 프로젝트를 만든다.
검사 기록, 작업자 확인 기록, 레시피, 작업자 정보를 저장하고 API로 제공한다.
DB에 직접 접근하는 모듈은 MES뿐이다. 테이블 생성 SQL은 `src/main/resources/db/`에 둔다.
