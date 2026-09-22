namespace Inspection.Contracts;

/// <summary>레시피 부품 하나 (정답지). MES에서 받아온다.</summary>
/// <param name="PartId">R1, U1, W1 ...</param>
/// <param name="ExpectedClass">있어야 할 종류</param>
/// <param name="Holes">차지해야 할 홀 전체</param>
/// <param name="ExpectedPinSide">IC만 "up"/"down", 그 외 null</param>
public record RecipePart(string PartId, string ExpectedClass, List<Hole> Holes, string? ExpectedPinSide);