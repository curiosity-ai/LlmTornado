using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.SlideView;

public class FantasySlide
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Content { get; set; }
    public string ImagePath { get; set; }
    public string AudioPath { get; set; }
    public string NextSlideId { get; set; }
    public string PreviousSlideId { get; }
}
