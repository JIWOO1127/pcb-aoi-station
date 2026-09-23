namespace Inspection.Contracts;

/// <summary>레시피 부품 하나 (정답지). MES 또는 JSON 파일에서 온다.</summary>
/// <param name="PartId">R1, U1, W1 ...</param>
/// <param name="ExpectedClass">있어야 할 종류 (모델 클래스 이름과 동일해야 함)</param>
/// <param name="Holes">차지해야 할 홀 전체</param>
public record RecipePart(string PartId, string ExpectedClass, List<Hole> Holes);

/// <summary>제품 하나의 레시피.</summary>
public record Recipe(string ProductId, int Version, List<RecipePart> Parts);