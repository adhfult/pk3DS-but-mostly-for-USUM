namespace pk3DS.Core.Randomizers.Competitive;

/// <summary>
/// The numeric constants section 0 of competitive_manifesto.md fixes, in one place.
/// <para>
/// The manifesto is normative, so any number it states has to exist somewhere the code actually
/// reads. Keeping them here rather than as literals at each use site is what stops the two drifting:
/// the retry budget had already diverged, with the document promising 12 bounded attempts while the
/// generator ran 30.
/// </para>
/// <para>
/// If a value here changes, the corresponding line of the manifesto changes with it. Neither is
/// allowed to move alone.
/// </para>
/// </summary>
public static class CompetitiveRules
{
    /// <summary>
    /// Manifesto 0.6: bounded retries for a constrained pick before constraints are dropped in
    /// reverse precedence order. Generation never stalls; it degrades.
    /// </summary>
    public const int ConstrainedPickAttempts = 30;

    /// <summary>Manifesto 0.2: weight for a plain SHOULD.</summary>
    public const int ShouldWeight = 4;

    /// <summary>Manifesto 0.2: weight for a SHOULD described as a "strong" or "heavy" bias.</summary>
    public const int StrongShouldWeight = 8;

    /// <summary>Manifesto 0.2: weight for a SHOULD qualified as "not so heavily".</summary>
    public const int WeakShouldWeight = 2;

    /// <summary>
    /// Weight for a candidate against a single SHOULD rule: the rule's weight when the candidate
    /// satisfies it, 1 when it does not. Weights of independent rules multiply, and a rule no
    /// candidate satisfies leaves the pool unweighted rather than empty.
    /// </summary>
    public static int Weigh(bool satisfies, int weight = ShouldWeight) => satisfies ? weight : 1;
}
