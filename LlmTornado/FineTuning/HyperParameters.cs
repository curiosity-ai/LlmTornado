﻿using Newtonsoft.Json;

namespace LlmTornado.FineTuning;

public sealed class HyperParameters
{
    [JsonProperty("n_epochs")]
    public int? Epochs { get; set; }

    [JsonProperty("batch_size")]
    public int? BatchSize { get; set; }

    [JsonProperty("learning_rate_multiplier")]
    public int? LearningRateMultiplier { get; set; }
}