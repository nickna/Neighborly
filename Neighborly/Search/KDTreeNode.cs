using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Search
{
    public class KDTreeNode
    {
        public Vector? Vector { get; set; }
        public KDTreeNode? Left { get; set; }
        public KDTreeNode? Right { get; set; }
    }
}
