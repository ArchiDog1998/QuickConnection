using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace QuickConnection
{
    public class QuickConnectionInfo : GH_AssemblyInfo
    {
        public override string Name => "QuickConnection";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "Fast connecting wires.";

        public override Guid Id => new Guid("6C0DDB78-4484-4481-996A-60CF4D9B90CE");

        //Return a string identifying you or your company.
        public override string AuthorName => "秋水";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "1123993881@qq.com";

        public override string Version => "0.9.0";
    }
}