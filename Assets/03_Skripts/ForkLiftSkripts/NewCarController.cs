using System;
using UnityEngine;

internal enum CarDriveType
{
    FrontWheelDrive,
    RearWheelDrive,
    FourWheelDrive
}

internal enum CarSteerType
{
    FrontSteerWheels,
    RearSteerWheels,
    FourSteerWheels
}

internal enum SpeedType
{
    MPH,
    KPH
}

public class NewCarController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private int colWheels;
    [SerializeField] private CarSteerType m_CarSteerType = CarSteerType.RearSteerWheels; // Gabelstapler lenken meist hinten
    [SerializeField] private CarDriveType m_CarDriveType = CarDriveType.FrontWheelDrive; // Gabelstapler haben Antrieb meist vorn
    [SerializeField] private WheelCollider[] m_WheelColliders = new WheelCollider[4];
    [SerializeField] private GameObject[] m_WheelMeshes = new GameObject[4];
    [SerializeField] private Vector3 m_CentreOfMassOffset;

    [Header("Performance")]
    [SerializeField] private float m_MaximumSteerAngle = 45f;
    [Range(0, 1)][SerializeField] private float m_SteerHelper = 0.5f;
    [SerializeField] private float m_FullTorqueOverAllWheels = 5000f; // Erhöht für schnellere Beschleunigung
    [SerializeField] private float m_ReverseTorque = 2500f; // Erhöht für schnelleres Rückwärtsfahren
    [SerializeField] private float m_MaxHandbrakeTorque = 10000f;
    [SerializeField] private float m_Downforce = 100f;
    [SerializeField] private float m_BrakeTorque = 3000f;

    [Header("Speed & Limits")]
    [SerializeField] private SpeedType m_SpeedType;
    [SerializeField] private float m_Topspeed = 30; // Gabelstapler sind nicht extrem schnell
    [SerializeField] private static int NoOfGears = 1; // Gabelstapler haben meist 1 Gang (Elektro/Hydrostat)
    [SerializeField] private float m_RevRangeBoundary = 1f;

    private Rigidbody m_Rigidbody;
    private float m_CurrentSteerAngle;

    // Speichert die ursprünglichen Drag-Werte
    private float defaultLinearDamping;
    private float defaultAngularDamping;

    public bool Skidding { get; private set; }
    public float BrakeInput { get; private set; }
    public float CurrentSteerAngle { get { return m_CurrentSteerAngle; } }
    public float CurrentSpeed { get { return m_Rigidbody.linearVelocity.magnitude * 2.23693629f; } }
    public float MaxSpeed { get { return m_Topspeed; } }
    public float Revs { get; private set; }
    public float AccelInput { get; private set; }

    private void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();

        // Masse-Schwerpunkt tief setzen für Stabilität
        m_Rigidbody.centerOfMass = m_CentreOfMassOffset;

        // Standard Damping Werte speichern (Unity 6 nutzt linearDamping statt drag)
        defaultLinearDamping = m_Rigidbody.linearDamping;
        defaultAngularDamping = m_Rigidbody.angularDamping;

        // Handbrems-Torque auf realistischen hohen Wert setzen (Float.Max verursacht manchmal Physik-Glitches)
        if (m_MaxHandbrakeTorque > 1000000) m_MaxHandbrakeTorque = 50000f;
    }

    public void Move(float steering, float accel, float footbrake, float handbrake)
    {
        UpdateWheelMeshes();

        // Inputs clampen
        steering = Mathf.Clamp(steering, -1, 1);
        AccelInput = accel = Mathf.Clamp(accel, 0, 1);
        BrakeInput = footbrake = -1 * Mathf.Clamp(footbrake, -1, 0);
        handbrake = Mathf.Clamp(handbrake, 0, 1);

        // 1. LENKUNG (Sofortige Reaktion)
        ApplySteering(steering);

        // 2. STABILISIERUNG
        SteerHelper();

        // 3. ANTRIEB (Sofortiges Drehmoment ohne Verzögerung)
        ApplyDrive(accel, footbrake);

        // 4. HANDBREMSE (Aggressiver Stop)
        ApplyHandbrake(handbrake);

        // 5. PHYSIK EXTRAS
        CapSpeed();
        AddDownForce();

        // Dummy Revs Berechnung (für Audio)
        Revs = Mathf.Lerp(Revs, 0.2f + Mathf.Abs(accel), Time.deltaTime * 5f);
    }

    private void UpdateWheelMeshes()
    {
        for (int i = 0; i < colWheels; i++)
        {
            Quaternion quat;
            Vector3 position;
            m_WheelColliders[i].GetWorldPose(out position, out quat);
            m_WheelMeshes[i].transform.position = position;
            m_WheelMeshes[i].transform.rotation = quat;
        }
    }

    private void ApplySteering(float steering)
    {
        m_CurrentSteerAngle = steering * m_MaximumSteerAngle;

        if (m_CarSteerType == CarSteerType.FrontSteerWheels)
        {
            m_WheelColliders[0].steerAngle = m_CurrentSteerAngle;
            m_WheelColliders[1].steerAngle = m_CurrentSteerAngle;
        }
        else if (m_CarSteerType == CarSteerType.RearSteerWheels)
        {
            // Bei Gabelstaplern lenken oft die Hinterräder (Index 2 und 3)
            m_WheelColliders[2].steerAngle = m_CurrentSteerAngle;
            m_WheelColliders[3].steerAngle = m_CurrentSteerAngle;
        }
        else if (m_CarSteerType == CarSteerType.FourSteerWheels)
        {
            for (int i = 0; i < 4; i++) m_WheelColliders[i].steerAngle = m_CurrentSteerAngle;
        }
    }

    private void ApplyDrive(float accel, float footbrake)
    {
        float thrustTorque = 0f;

        // Direktes Drehmoment berechnen (Ohne langsame TractionControl Ramp-up)
        if (m_CarDriveType == CarDriveType.FourWheelDrive)
            thrustTorque = accel * (m_FullTorqueOverAllWheels / 4f);
        else
            thrustTorque = accel * (m_FullTorqueOverAllWheels / 2f);

        // Antrieb anwenden
        for (int i = 0; i < colWheels; i++)
        {
            bool isDrivenWheel = false;

            if (m_CarDriveType == CarDriveType.FourWheelDrive) isDrivenWheel = true;
            else if (m_CarDriveType == CarDriveType.FrontWheelDrive && (i == 0 || i == 1)) isDrivenWheel = true;
            else if (m_CarDriveType == CarDriveType.RearWheelDrive && (i == 2 || i == 3)) isDrivenWheel = true;

            if (isDrivenWheel)
            {
                m_WheelColliders[i].motorTorque = thrustTorque;
            }

            // Fußbremse & Rückwärtsgang Logik
            if (footbrake > 0)
            {
                // Prüfen, ob wir uns tatsächlich vorwärts bewegen (lokale Z-Geschwindigkeit)
                float forwardSpeed = transform.InverseTransformDirection(m_Rigidbody.linearVelocity).z;

                // Wenn wir vorwärts rollen (> 1 unit), bremsen wir.
                // Wenn wir stehen oder bereits rückwärts rollen, geben wir Rückwärts-Gas.
                if (forwardSpeed > 0.5f)
                {
                    m_WheelColliders[i].brakeTorque = m_BrakeTorque * footbrake;
                    m_WheelColliders[i].motorTorque = 0f; // Sicherstellen, dass kein Motor gegen die Bremse arbeitet
                }
                else
                {
                    m_WheelColliders[i].brakeTorque = 0f;
                    m_WheelColliders[i].motorTorque = -m_ReverseTorque * footbrake;
                }
            }
            else
            {
                m_WheelColliders[i].brakeTorque = 0f;
            }
        }
    }

    private void ApplyHandbrake(float handbrakeInput)
    {
        if (handbrakeInput > 0.1f)
        {
            // 1. Radbremse
            var hbTorque = handbrakeInput * m_MaxHandbrakeTorque;
            m_WheelColliders[2].brakeTorque = hbTorque;
            m_WheelColliders[3].brakeTorque = hbTorque;
            m_WheelColliders[0].brakeTorque = hbTorque; // Alle Räder bremsen für Gabelstapler
            m_WheelColliders[1].brakeTorque = hbTorque;

            // 2. PHYSIK HACK: Sofortiges Stoppen durch erhöhten Widerstand (Drag)
            // Dies verhindert das "Rutschen" und stoppt das Fahrzeug extrem schnell.
            m_Rigidbody.linearDamping = 10f;
            m_Rigidbody.angularDamping = 10f;
        }
        else
        {
            // Reset auf Normalwerte
            m_Rigidbody.linearDamping = defaultLinearDamping;
            m_Rigidbody.angularDamping = defaultAngularDamping;

            // BrakeTorque wird in ApplyDrive zurückgesetzt, wenn Footbrake 0 ist
        }
    }

    private void CapSpeed()
    {
        float speed = m_Rigidbody.linearVelocity.magnitude;
        float limit = (m_SpeedType == SpeedType.MPH) ? m_Topspeed / 2.2369f : m_Topspeed / 3.6f;

        if (speed > limit)
        {
            m_Rigidbody.linearVelocity = limit * m_Rigidbody.linearVelocity.normalized;
        }
    }

    private void SteerHelper()
    {
        // Verhindert seitliches Rutschen bei schnellen Kurven (Arcade-Feeling)
        for (int i = 0; i < 4; i++)
        {
            WheelHit wheelhit;
            m_WheelColliders[i].GetGroundHit(out wheelhit);
            if (wheelhit.normal == Vector3.zero) return;
        }

        if (Mathf.Abs(transform.eulerAngles.y - m_Rigidbody.angularVelocity.y) < 10f)
        {
            var turnadjust = (transform.eulerAngles.y - m_Rigidbody.angularVelocity.y) * m_SteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnadjust, Vector3.up);
            // Sanftere Korrektur
            m_Rigidbody.linearVelocity = Vector3.Lerp(m_Rigidbody.linearVelocity, velRotation * m_Rigidbody.linearVelocity, Time.deltaTime * 10f);
        }
    }

    private void AddDownForce()
    {
        m_WheelColliders[0].attachedRigidbody.AddForce(-transform.up * m_Downforce * m_WheelColliders[0].attachedRigidbody.linearVelocity.magnitude);
    }
}