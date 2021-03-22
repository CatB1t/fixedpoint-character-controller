﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics.FixedPoint;

[RequireComponent(typeof(CapsuleCollider))]
public class CustomCharacterMotor : MonoBehaviour
{

    #region Inspector Showing
    public float InternalAngleRead = 0; // TODO Used-Debug
    public Vector3 InternalPositionRead = new Vector3(0, 0, 0);// TODO Used-Debug
    #endregion

    #region Player Properties
    [Header("Player Properties")]
    [SerializeField] private float m_playerWalkSpeed = 2f;
    [SerializeField] private float m_playerRotationSpeed = 30f;

    [Header("Physics")]
    [SerializeField] private float m_gravityForce = -9.81f;
    [SerializeField, Tooltip("0.1 for almost no movement along the wall, 1 for same speed]"), Range(0.1f,1)] private float m_wallFraction = 0.4f;

    [Header("Collider Properties")]
    [SerializeField] private float m_capusleRadius = 0.5f;
    [SerializeField] private float m_capsuleHeight = 2;
    [SerializeField] private float m_skinWidth = 0.08f;
    [SerializeField] private float m_maxSlopeAngle= 45;
    [SerializeField] private LayerMask m_groundMask;
    [SerializeField] private LayerMask m_colliderObjectsMask;
    [SerializeField] private LayerMask m_excludePlayer;
    #endregion

    #region Cached Variables
    private fp m_fpFixedDeltaTime;
    private fp m_fpWalkSpeed;
    private fp m_fpRotationSpeed;
    private fp m_fpWallFraction;
    private fp m_fpGravityForce;
    private float m_checkDistanceGround;
    #endregion

    #region Player Flags
    private bool m_isGrounded = false;
    #endregion

    #region Internal Transform
    public readonly static fp InternalFullTurn = new fp(360);
    public readonly static fp Internal2Radian = (fp) 0.0174532924f;
    public readonly static fp3 FpUpVector = new fp3(0,1,0);
    private fp m_internalAngle = new fp(0);
    private fp2 m_internalInput = new fp2(0,0);
    private fp3 m_internalVelocity = new fp3(0, 0,0);
    private fp3 m_internalGravityForce = new fp3(0,0,0);
    private fp3 m_internalPosition = new fp3(0, 0, 0);
    private fp3 m_internalForwardVector = new fp3(0,0,0);
    private fp3 m_internalRightVector = new fp3(0,0,0);
    #endregion

    #region Debug Methods
    /// <summary>
    /// Callback to draw gizmos that are pickable and always drawn.
    /// </summary>
    Vector3 debugGroundCheckPoint = new Vector3(0,0,0);
    Vector3 debugRayPoint = Vector3.zero;
    Vector3 debugTopPoint = Vector3.zero;
    Vector3 debugBottomPoint = Vector3.zero;
    void OnDrawGizmos()
    {
        if(EditorApplication.isPlaying)
        {
            //Gizmos.DrawSphere(debugGroundCheckPoint, m_capusleRadius);
            Gizmos.DrawSphere(debugTopPoint, m_capusleRadius);
            Gizmos.DrawSphere(debugBottomPoint, m_capusleRadius);
            Gizmos.DrawRay(debugRayPoint, Vector3.down);
        }
    }

    string debugCurrentHitGround = "";
    string debugCurrentHitNormal = "";
    void OnGUI()
    {
        if(m_isGrounded)
        {
            GUI.color = Color.green;
            GUI.Label(new Rect(0, 0, 200, 70), "Ground:" + debugCurrentHitGround);
            GUI.color = Color.red;
            GUI.Label(new Rect(0, 80, 200, 70), "Ground Normal:" + debugCurrentHitNormal);
        }
    }
    #endregion

    void OnValidate()
    {
        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        collider.hideFlags = HideFlags.NotEditable;
        collider.radius = m_capusleRadius;
        collider.height = m_capsuleHeight;
    }

    void Awake()
    {
        fp3 tmpPlayerPosition = new fp3( (fp) transform.localPosition.x, (fp) transform.localPosition.y, (fp) transform.localPosition.z); 
        m_internalPosition = tmpPlayerPosition;
        ApplyTransform();
    }

    void Start()
    {
        CacheVariables();
    }

    void CacheVariables()
    {
        m_checkDistanceGround = ((m_capsuleHeight / 2f) - m_capusleRadius) + m_skinWidth; 
        m_fpFixedDeltaTime = (fp) Time.fixedDeltaTime;
        m_fpRotationSpeed = (fp) m_playerRotationSpeed;
        m_fpWallFraction = (fp) m_wallFraction;
        m_fpWalkSpeed = (fp) m_playerWalkSpeed;
        m_fpGravityForce = (fp) m_gravityForce;
    }

    void Update() // TODO // Used for debugging
    {
        if (EditorApplication.isPlaying)
        {
            InternalAngleRead = (float) m_internalAngle;
            InternalPositionRead = new Vector3((float) m_internalPosition.x, (float) m_internalPosition.y, (float) m_internalPosition.z);
        }
    }

    /// <summary>
    /// This function is called every fixed framerate frame, if the MonoBehaviour is enabled.
    /// </summary>
    void FixedUpdate()
    {
        CheckForGround();
        ApplyGravity();
        ComputeTransform();
        TryMove(m_internalInput);
        ApplyTransform();
        ClearState();
    }

    void CheckForGround() // TODO, investigate bug when walking off slopes it fails
    {
        debugGroundCheckPoint = transform.localPosition + Vector3.down * (m_checkDistanceGround);
        // SphereCast
        bool hitFound = Physics.SphereCast(transform.localPosition, m_capusleRadius, Vector3.down, out RaycastHit sphereCastHit , m_checkDistanceGround, m_groundMask);
        // SphereOverlap and check for collisions
        if(hitFound && sphereCastHit.distance < m_checkDistanceGround) // TODO check for max slope angle
        {
            // if there's a hit found, sweep for the correct ground
            Collider[] hitList = new Collider[5];
            int num = Physics.OverlapSphereNonAlloc(sphereCastHit.point, m_capusleRadius, hitList, m_groundMask);
            Vector3 currentNormal = Vector3.zero;
            int validHitIndex = -1;
            if(num > 0)
            {
                float maxDistance = Mathf.Infinity;
                for(int i = 0; i < num; ++i)
                {
                    debugRayPoint = transform.localPosition;
                    if(hitList[i].Raycast(new Ray(transform.localPosition, Vector3.down), out RaycastHit info, 2f))
                    {
                        if(info.distance < maxDistance)
                        {
                            maxDistance = info.distance;
                            currentNormal = info.normal;
                            validHitIndex = i;
                        }
                    }
                }
            }

            if (validHitIndex != -1)
            {
                // if valid hit is found 
                m_internalGravityForce = new fp3(0, 0, 0);
                m_isGrounded = true;
                debugCurrentHitGround = sphereCastHit.transform.name;
            }
        }
        else
        {
            m_isGrounded = false;
        }
    }
    private fp3 SweepForGroundNormal(fp3 direction)
    {
        fp3 groundNormal  = new fp3(0,0,0);
        Vector3 rayPoint = transform.localPosition + (Vector3.up * m_capsuleHeight / 2) + FixedVector3ToVector3(direction);
        debugRayPoint = rayPoint;
        Ray ray = new Ray(rayPoint, Vector3.down);
        bool raycastHit = Physics.Raycast(ray, out RaycastHit rayHitInfo , 4f);
        Collider[] hitList = new Collider[5];
        int raycast = Physics.OverlapSphereNonAlloc(rayHitInfo.point, m_capusleRadius,  hitList, m_groundMask); // TODO use Sphere/Capsule cast
        float capsuleDistance = m_capsuleHeight / 2 + (m_capusleRadius);
        if(raycast > 0)
        {
            float maxDistance = Mathf.Infinity;
            int indexOfValidHit = -1;

            for(int i = 0; i < raycast; i++)
            {
                if (hitList[i].Raycast(new Ray(transform.localPosition, Vector3.down), out RaycastHit hitInfo, m_groundMask))
                {
                    if (hitInfo.distance < maxDistance)
                    {
                        indexOfValidHit = i;
                        groundNormal = Vector3ToFixedVector(hitInfo.normal);
                    }
                }
            }
            if(indexOfValidHit != -1)
            {
                debugCurrentHitNormal = hitList[indexOfValidHit].transform.name;
            }
        }
        return groundNormal;
    }

    private void ApplyGravity() 
    {
        if(!m_isGrounded)
        {
            m_internalGravityForce += new fp3(0, (m_fpGravityForce) * (m_fpFixedDeltaTime * m_fpFixedDeltaTime), 0);
        }
    }

    private void ComputeTransform()
    {
        fp angleInRad = m_internalAngle * Internal2Radian;
        fp sinCoordinate = fpmath.sin(angleInRad);
        fp cosCoordinate = fpmath.cos(angleInRad);

        m_internalForwardVector = new fp3(sinCoordinate, 0, cosCoordinate);
        m_internalRightVector = fpmath.cross(FpUpVector, m_internalForwardVector); // TODO no need to calc if no right/left input
    }

    private void ApplyTransform()
    {
        m_internalPosition += m_internalVelocity + m_internalGravityForce;
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, InternalAngleRead, transform.localEulerAngles.z );
        transform.localPosition = new Vector3((float) m_internalPosition.x, (float) m_internalPosition.y, (float) m_internalPosition.z);
    }

    private void ClearState()
    {
        m_internalInput = new fp2(0,0);
        m_internalVelocity = new fp3(0,0,0);
    }

    private bool CanMoveInDirection(Vector3 direction, float distance, out Vector3 wallNormal) 
    {
        float circleOffset = (m_capsuleHeight / 2) - m_capusleRadius;
        Vector3 topPoint = transform.localPosition + Vector3.up * circleOffset;
        debugTopPoint = topPoint;
        Vector3 bottomPoint = transform.localPosition - Vector3.up * circleOffset;
        debugBottomPoint = bottomPoint;
        bool hitFound = Physics.CapsuleCast(topPoint, bottomPoint, m_capusleRadius, direction, out RaycastHit hitInfo, 5f, m_excludePlayer);
        if(hitFound && hitInfo.distance < distance + m_skinWidth)
        {
            wallNormal = hitInfo.normal;
            return  false;
        }
        wallNormal = Vector3.zero;
        return true;
    }

    private void TryMove(fp2 direction)
    {
        if(FpVector2SqrMagn(direction) <= (fp)0.01)
            return;

        fp2 fixedDirection = new fp2((fp)direction.x, (fp)direction.y);
        fp3 desiredDirection = new fp3(0, 0, 0);

        fixedDirection = fpmath.normalize(fixedDirection);

        desiredDirection = (fixedDirection.x * m_internalRightVector) + (fixedDirection.y * m_internalForwardVector);
        fp3 groundNormal = SweepForGroundNormal(desiredDirection * m_fpFixedDeltaTime); 
        desiredDirection = ProjectVectorOntoPlane(desiredDirection, groundNormal); // Project onto ground, to move parrelel to ground

        // if there's wall slide along it 
        float distanceOfMovement = Time.fixedDeltaTime * m_playerWalkSpeed; // TODO move out speed to Controller
        bool canMoveInDir = CanMoveInDirection(FixedVector3ToVector3(desiredDirection), distanceOfMovement, out Vector3 wallNormal);

        if (!canMoveInDir)
        {
            desiredDirection = ProjectVectorOntoPlane(desiredDirection, new fp3((fp)wallNormal.x, (fp)wallNormal.y, (fp)wallNormal.z));
            Debug.Log("Trying to walk against wall");
            if (!CanMoveInDirection(FixedVector3ToVector3(desiredDirection), distanceOfMovement * m_wallFraction, out Vector3 dummyWallNormal))
            {
                Debug.Log("Can't move.");
                return;
            }
        }

        fp speed = m_fpFixedDeltaTime * m_fpWalkSpeed * (canMoveInDir ?  fp.one : m_fpWallFraction);
        m_internalVelocity += (desiredDirection) * speed;
    }

    #region Api
    /// <summary>
    /// Moves relative to the player's transform,  doesn't apply gravity
    /// </summary>
    public void Move(Vector2 direction)
    {

        if(direction.sqrMagnitude <= Mathf.Epsilon) // Zero Input
            return;

        m_internalInput += (new fp2((fp)direction.x, (fp) direction.y));
    }

    ///<summary>
    /// Rotate the character around the y-axis 
    ///</summary>
    public void Rotate(float rotationDelta) // TODO investigate rotaion bug
    {
        fp rotationDeltaFixed = (fp) rotationDelta;
        m_internalAngle += rotationDeltaFixed;

        if (m_internalAngle >= InternalFullTurn)
        {
            m_internalAngle %= InternalFullTurn;
        }
        else if (m_internalAngle < 0) 
        {
            fp reminder = fpmath.abs(m_internalAngle % InternalFullTurn);
            m_internalAngle = InternalFullTurn - reminder;
        }
    }
    #endregion

    #region Helpers // TODO seperate
    public fp3 ProjectVectorOntoPlane(fp3 vector, fp3 planeNormal)
    {
        // Formula: ( (v * w) / w^2 ) * w
        if(FpVector3SqrMagn(vector) < (fp) 0.001f)
        {
            return vector;
        }
        fp3 normalizedVector = fpmath.normalize(vector);
        fp dot = fpmath.dot(normalizedVector, planeNormal);
        if(dot == 0) return vector; // If orthogonal

        fp normalMagnitude = fpmath.rsqrt( FpVector3SqrMagn(vector) );
        fp3 projection = ((dot) / normalMagnitude * normalMagnitude) * planeNormal;

        return normalizedVector - projection;
    }

    public fp FpVector3SqrMagn(fp3 vector)
    {
        return (vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
    }
    
    public fp FpVector2SqrMagn(fp2 vector)
    {
        return (vector.x * vector.x + vector.y * vector.y);
    }

    public Vector3 FixedVector3ToVector3(fp3 vector)
    {
        return new Vector3((float) vector.x, (float) vector.y,(float) vector.z);
    }

    public fp3 Vector3ToFixedVector(Vector3 vector)
    {
        return new fp3((fp) vector.x, (fp) vector.y, (fp) vector.z);
    }
    #endregion
}
