using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlStyles
{
    public class UiLabelForGrid : TextBlock
    {
#if DEBUG
    ~UiLabelForGrid() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif


    }
}
