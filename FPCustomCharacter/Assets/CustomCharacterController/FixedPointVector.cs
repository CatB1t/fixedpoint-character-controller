using UnityEngine;
using Unity.Mathematics.FixedPoint;

public class FixedPointVector : MonoBehaviour
{
    public static fp3 ProjectVectorOntoPlane(fp3 vector, fp3 planeNormal)
    {
        // Formula: ( (v * w) / w^2 ) * w
        if(SqrMagn(vector) < (fp) 0.001f)
        {
            return vector;
        }
        fp3 normalizedVector = fpmath.normalize(vector);
        fp dot = fpmath.dot(normalizedVector, planeNormal);
        if(dot == 0) return vector; // If orthogonal

        fp normalMagnitude = fpmath.rsqrt( SqrMagn(vector) );
        fp3 projection = ((dot) / normalMagnitude * normalMagnitude) * planeNormal;

        return normalizedVector - projection;
    }

    public static fp SqrMagn(fp3 vector)
    {
        return (vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
    }
    
    public static fp SqrMagn(fp2 vector)
    {
        return (vector.x * vector.x + vector.y * vector.y);
    }

    public static Vector3 ToVector(fp3 vector)
    {
        return new Vector3((float) vector.x, (float) vector.y,(float) vector.z);
    }
    public static Vector2 ToVector(fp2 vector)
    {
        return new Vector2((float) vector.x, (float) vector.y);
    }

    public static fp3 ToFixedVector(Vector3 vector)
    {
        return new fp3((fp) vector.x, (fp) vector.y, (fp) vector.z);
    }

    public static fp2 ToFixedVector(Vector2 vector)
    {
        return new fp2((fp) vector.x, (fp) vector.y);
    }
}
