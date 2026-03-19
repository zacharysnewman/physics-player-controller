using UnityEngine;

namespace ZacharysNewman.PPC
{
    public interface IVelocityLayer
    {
        Vector3 GetVelocityContribution(float deltaTime);
        bool IsActive { get; }
        bool IsExclusive { get; }
    }
}
