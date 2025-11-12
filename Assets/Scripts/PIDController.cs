using UnityEngine;

[System.Serializable]
public class PIDController
{
    public float pGain, iGain, dGain;
    private float integral;
    private float lastError;

    public PIDController(float p, float i, float d)
    {
        this.pGain = p;
        this.iGain = i;
        this.dGain = d;
    }

    public float Update(float error, float deltaTime)
    {
        float pTerm = pGain * error;
        integral += error * deltaTime;
        float iTerm = iGain * integral;
        float derivative = (error - lastError) / deltaTime;
        lastError = error;
        float dTerm = dGain * derivative;
        return pTerm + iTerm + dTerm;
    }
}