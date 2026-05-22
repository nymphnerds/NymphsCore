using System.Collections.Generic;

namespace NymphsCoreManager.Models;

public sealed record NymphModuleDetailPrimaryActionInfo(
    NymphModuleActionInfo Action,
    string Heading,
    string Help,
    IReadOnlyList<string> ShowWhenStates);
