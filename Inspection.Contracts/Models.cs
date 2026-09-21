using System;
using System.Collections.Generic;
using System.Text;
// 데이터 모양들을 모아둔 파일 (using Inspection.Contracts;라고 쓰면 가져다 쓸 수 있음.)
namespace Inspection.Contracts;

public enum Verdict { Pass, Fail, Invalid }

/// <summary>불량 종류. 스테이션의 규칙 엔진이 탐지 결과와 레시피를 비교해 결정한다.</summary>
public enum DefectType
{
    Missing,        // 레시피 위치 근처에 탐지 없음
    Offset,         // 종류는 맞지만 레시피 위치에서 허용 오차 이상 벗어남
    WrongPart,      // 다른 종류가 꽂힘 (예: 470Ω 자리에 330Ω)
    Extra,          // 레시피에 없는 위치에서 탐지됨
    WireColorSwap   // 전선 색이 바뀜 (전원 +/- 반전 위험)
}

/// <summary>레시피 부품 하나의 판정 결과.</summary>
/// <param name="PartId">레시피 부품 ID (예: R1, U1, W3)</param>
/// <param name="ExpectedClass">레시피상 있어야 할 종류</param>
/// <param name="DetectedClass">실제로 탐지된 종류. 누락이면 null</param>
/// <param name="Confidence">탐지 신뢰도. 누락이면 0</param>
/// <param name="OffsetMm">레시피 위치와의 거리. 전선·누락이면 null</param>
/// <param name="Defect">불량 종류. 정상이면 null</param>
/// <param name="Verdict">이 부품의 판정</param>
/// <param name="X">표시용 박스. 정렬된 이미지 기준 픽셀</param>
public record PartResult(string PartId, string ExpectedClass, string? DetectedClass,
    double Confidence, double? OffsetMm, DefectType? Defect, Verdict Verdict,
    int X, int Y, int W, int H);

/// <summary>보드 한 장의 검사 결과. 스테이션이 만들어 HMI와 MES로 보낸다.</summary>
/// <param name="InspectionId">스테이션이 생성하는 고유번호. MES 재전송 시 중복 저장을 막는 키</param>
/// <param name="Verdict">보드 전체 판정. 불량 부품이 하나라도 있으면 Fail</param>
/// <param name="DefectCount">불량 부품 수 (추가 부품 포함)</param>
/// <param name="Parts">레시피 부품별 결과. Invalid면 빈 목록</param>
/// <param name="ImageUrl">정렬된 검사 이미지 주소. 박스 좌표가 이 이미지 기준</param>
/// <param name="LatencyMs">트리거부터 결과까지 걸린 시간. 택트타임 지표</param>
/// <param name="ErrorCode">Invalid 사유 (MARKER_NOT_FOUND, CAPTURE_QUALITY 등). 정상이면 null</param>
public record InspectionResult(Guid InspectionId, Verdict Verdict, int DefectCount,
    List<PartResult> Parts, string? ImageUrl, int LatencyMs, string? ErrorCode);
