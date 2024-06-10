using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Search
{
    public class BallTreeNode 
    {
        public Vector Center { get; set; }
        public double Radius { get; set; }
        public BallTreeNode Left { get; set; }
        public BallTreeNode Right { get; set; }
    }
}
