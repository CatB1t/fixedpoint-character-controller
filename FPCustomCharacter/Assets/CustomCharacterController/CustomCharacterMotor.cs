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
    [SerializeField] private float m_gravityForce = -9.81f;
    [SerializeField] private float m_playerWalkSpeed = 2f;
    [SerializeField] private float m_playerRotationSpeed = 30f;

    [Header("Collider Properties")]
    [SerializeField] private float m_capusleRadius = 0.5f;
    [SerializeField] private float m_capsuleHeight = 2;
    [SerializeField] private float m_skinWidth = 0.1f;
    [SerializeField] private LayerMask m_groundMask;
    #endregion

    #region Cached Variables
    private fp m_fpFixedDeltaTime;
    private fp m_fpWalkSpeed;
    private fp m_fpRotationSpeed;
    private float m_checkDistanceGround;
    #endregion

    #region Player Flags
    bool m_isGrounded = false;
    #endregion

    #region Internal Transform
    public readonly static fp InternalFullTurn = new fp(360);
    public readonly static fp Internal2Radian = (fp) 0.0174532924f;
    public readonly static fp3 FpUpVector = new fp3(0,1,0);
    private fp m_internalAngle = new fp(0);
    private fp3 m_internalPosition = new fp3(0, 2, 0);
    private fp3 m_internalGroundNormal = new fp3(0,0,0);
    private fp3 m_internalForwardVector = new fp3(0,0,0);
    private fp3 m_internalRightVector = new fp3(0,0,0);
    #endregion

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    void OnValidate()
    {
        CapsuleCollider collider = GetComponent<CapsuleCollider>();
        collider.hideFlags = HideFlags.NotEditable;
        collider.radius = m_capusleRadius;
        collider.height = m_capsuleHeight;
    }

    void Awake()
    {
        fp3 tmpPlayerPosition = new fp3((fp) transform.localPosition.x, (fp) transform.localPosition.y, (fp) transform.localPosition.z); 
        m_internalPosition = tmpPlayerPosition;
        ApplyTransform();
    }

    void Start()
    {
        CacheVariables();
    }

    void CacheVariables()
    {
        m_fpFixedDeltaTime = (fp) Time.fixedDeltaTime;
        m_fpWalkSpeed = (fp) m_playerWalkSpeed;
        m_fpRotationSpeed = (fp) m_playerRotationSpeed;
        m_checkDistanceGround = ((m_capsuleHeight / 2f) - m_capusleRadius) + m_skinWidth; 
    }

    void Update() // TODO / Used for debugging
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
        // Update is gronded
        CheckForGround();
        // Apply gravity 
        ApplyGravity();
        ApplyTransform();
    }

    void CheckForGround()
    {
        // SphereCollider from the bottom of the capsule
        Vector3 startPosition = transform.localPosition;
        Vector3 endPosition = startPosition + Vector3.down * (m_capsuleHeight / 2f);
        Ray ray = new Ray(startPosition, endPosition);

        bool sphereHit = Physics.SphereCast(startPosition, m_capusleRadius, Vector3.down, out RaycastHit sphereCastHit , m_checkDistanceGround, m_groundMask);
        m_isGrounded = sphereHit ? true : false; // Update is grounded

        if(sphereHit) // TODO check if ground isn't slippery
        {
            // TODO set ground vector to normalize to
            m_internalGroundNormal = new fp3((fp) sphereCastHit.normal.x, (fp) sphereCastHit.normal.y, (fp) sphereCastHit.normal.z);
        }
        else
        {
            m_internalGroundNormal = new fp3(0,0,0);
        }
    }

    private void ApplyGravity() 
    {
        if(!m_isGrounded)
        {
            m_internalPosition += new fp3(0, (fp) m_gravityForce * m_fpFixedDeltaTime, 0); // TODO use proper gravity equation, TODO cache Gravity force into fp, TODO proper add gravity velocity and add it at the end
        }
    }

    private void ComputeTransform()
    {
        // Compute internal forward and right vectors
    }

    private void ApplyTransform()
    {
        transform.localRotation = Quaternion.Euler(transform.localRotation.x, (int) m_internalAngle, transform.localRotation.z ); // TODO Quaterinon issue with rotation
        transform.localPosition = new Vector3((float) m_internalPosition.x, (float) m_internalPosition.y, (float) m_internalPosition.z);
    }

    #region Api
    /// <summary>
    /// Moves relative to the player's transform
    ///, doesn't apply gravity
    /// </summary>
    public void Move(Vector2 direction)
    {

        if(direction.sqrMagnitude <= 0) 
            return;

        // Check if we can move in the direction, if not. return
        
        fp2 fixedDirection = new fp2((fp) direction.x, (fp) direction.y );
        //fpmath.normalize(fixedDirection);
        // Calculate forward direction
        fp angleInRad = m_internalAngle * Internal2Radian;

        fp sinCoordinate = fpmath.sin(angleInRad);
        fp cosCoordinate = fpmath.cos(angleInRad);

        fp3 forwardVector = new fp3(sinCoordinate, 0 , cosCoordinate);
        fp3 rightVector = fpmath.cross(FpUpVector, forwardVector); // TODO no need to calc if no right/left input

        fp3 desiredDirection = (fixedDirection.x * rightVector) + (fixedDirection.y * forwardVector);

        // Project onto ground, to move parrelel to ground
        desiredDirection = ProjectVectorOntoPlane(desiredDirection, m_internalGroundNormal);

        m_internalPosition += (desiredDirection * m_fpFixedDeltaTime * m_fpWalkSpeed);
    }

    ///<summary>
    /// Rotate the character around the y-axis 
    ///</summary>
    public void Rotate(float rotationDelta)
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
    fp3 ProjectVectorOntoPlane(fp3 vector, fp3 planeNormal)
    {
        // Formula: 
        // ((v * w)/w^2)*w

        fp3 normalizedVector = fpmath.normalize(vector);
        fp dot = fpmath.dot(normalizedVector, planeNormal);
        if(dot == 0) return normalizedVector;

        fp mangtidueOfNormal = fpmath.sqrt( ( (planeNormal.x * planeNormal.x) + (planeNormal.y * planeNormal.y) + (planeNormal.z * planeNormal.z) ) );
        fp3 projection = ((dot) / mangtidueOfNormal * mangtidueOfNormal) * planeNormal;
        fp3 actualProjection = normalizedVector - projection;

        return actualProjection;
    }
    #endregion
}
