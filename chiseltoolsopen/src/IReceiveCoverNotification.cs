using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chiseltools.src
{
    public interface IReceiveCoverNotification
    {
        void CoverChanged();
        bool SkipShapeRendering { get; }
        bool SkipCollisionBoxGeneration { get; }
    }
}
