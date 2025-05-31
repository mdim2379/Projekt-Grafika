public class CubeArrangementModel
{
    public bool AnimationEnabeld { get; set; } = true;
    private double Time { get; set; } = 0;
    
    public double CenterCubeOrbitAngle { get; private set; } = 0;

    internal void AdvanceTime(double deltaTime)
    {
        if (!AnimationEnabeld) return;
        
        Time += deltaTime;
        
        CenterCubeOrbitAngle = Time * 10; 
    }
}