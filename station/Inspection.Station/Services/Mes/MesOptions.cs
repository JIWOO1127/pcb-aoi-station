namespace Inspection.Station.Services.Mes;

/// <summary>appsettings.json의 "Mes" 섹션.</summary>
public class MesOptions
{
    /// <summary>false면 검사 기록을 MES로 보내지도, 쌓지도 않는다 (MES 없이 개발할 때). 로그인은 이 값과 무관하게 BaseUrl로 시도한다.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>MES 주소. 같은 노트북이면 http://localhost:8080, 다른 노트북이면 그 IP.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>작업자 로그인 API 경로 (MES MVP-26).</summary>
    public string LoginPath { get; set; } = "/api/auth/login";

    /// <summary>검사 기록을 받는 API 경로 (MES 담당자와 확정 후 맞춘다).</summary>
    public string InspectionsPath { get; set; } = "/api/inspections";

    /// <summary>
    /// 스테이션 인증 키. MES가 X-Station-Key 헤더로 확인한다.
    /// 작업자 JWT(1시간 만료) 대신 쓰는 이유: MES가 끊긴 동안 쌓인 기록을 몇 시간 뒤에 보내도 거절되지 않게.
    /// </summary>
    public string StationKey { get; set; } = "dev-station-key";

    /// <summary>이 스테이션의 이름. 여러 대가 한 MES에 보낼 때 구분용.</summary>
    public string StationId { get; set; } = "ST-01";

    /// <summary>못 보낸 기록을 다시 시도하는 간격(초).</summary>
    public int RetrySeconds { get; set; } = 5;

    /// <summary>MES 호출 제한 시간(초). 짧게 둬야 MES가 멈췄을 때 전송이 오래 붙잡히지 않는다.</summary>
    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>쌓아둘 폴더 (실행 폴더 기준). 파일 하나 = 검사 기록 하나.</summary>
    public string OutboxFolder { get; set; } = "mes-outbox";

    /// <summary>
    /// 로그인할 때 MES의 승인(활성) 레시피를 받아 recipes-cache에 둘지 (원본 Recipes 폴더는 건드리지 않는다).
    /// 끄면 원본 레시피 파일만 쓰고 /api/login의 recipeSync는 빈 목록. 켜도 MES가 안 되면 지금 레시피로 그대로 검사한다.
    /// </summary>
    public bool RecipeSyncEnabled { get; set; } = true;

    /// <summary>MES 레시피 API 경로 (목록 GET {경로}, 상세 GET {경로}/{recipeId}). 작업자 JWT로 호출한다.</summary>
    public string RecipesPath { get; set; } = "/api/recipes";

    /// <summary>로그인 때 레시피 동기화에 쓰는 최대 시간(초). 넘으면 동기화만 포기하고 로그인은 성공시킨다.</summary>
    public int RecipeSyncTimeoutSeconds { get; set; } = 3;
}
