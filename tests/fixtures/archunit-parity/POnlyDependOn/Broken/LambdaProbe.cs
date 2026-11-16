using System;
using Parity.POnlyDependOn.Allowed.Target;

namespace Parity.POnlyDependOn.Broken;

public class LambdaProbe
{
    public Repository? Store;

    public int Count(int step)
    {
        var total = 0;
        Action add = () => total += step;
        add();
        add();
        return total;
    }
}
