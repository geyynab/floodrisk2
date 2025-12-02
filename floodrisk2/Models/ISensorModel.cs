namespace floodrisk2.Models
{
    public interface ISensorModel
    {
        // generate time-domain vector for time vector t (seconds)
        double[] Generate(double[] t);

        // human readable name & formula short desc
        string Name { get; }
        string Formula { get; }
    }
}
