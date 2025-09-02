using UnityEngine;
using System;
using System.Collections.Generic;

namespace Obi
{
    [AddComponentMenu("Physics/Obi/Obi Bone Override", 882)]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class ObiBoneOverride : MonoBehaviour
    {
        [SerializeField] protected ObiBone.BonePropertyCurve _radius = new ObiBone.BonePropertyCurve(0.1f, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _mass = new ObiBone.BonePropertyCurve(0.1f, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _rotationalMass = new ObiBone.BonePropertyCurve(0.1f, 1);

        // skin constraints:
        [SerializeField] protected ObiBone.BonePropertyCurve _skinCompliance = new ObiBone.BonePropertyCurve(0.01f, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _skinRadius = new ObiBone.BonePropertyCurve(0.1f, 1);

        // distance constraints:
        [SerializeField] protected ObiBone.BonePropertyCurve _stretchCompliance = new ObiBone.BonePropertyCurve(0, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _shear1Compliance = new ObiBone.BonePropertyCurve(0, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _shear2Compliance = new ObiBone.BonePropertyCurve(0, 1);

        // bend constraints:
        [SerializeField] protected ObiBone.BonePropertyCurve _torsionCompliance = new ObiBone.BonePropertyCurve(0, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _bend1Compliance = new ObiBone.BonePropertyCurve(0, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _bend2Compliance = new ObiBone.BonePropertyCurve(0, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _plasticYield = new ObiBone.BonePropertyCurve(0, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _plasticCreep = new ObiBone.BonePropertyCurve(0, 1);

        // aerodynamics
        [SerializeField] protected ObiBone.BonePropertyCurve _drag = new ObiBone.BonePropertyCurve(0.05f, 1);
        [SerializeField] protected ObiBone.BonePropertyCurve _lift = new ObiBone.BonePropertyCurve(0.02f, 1);

        /// <summary>  
        /// Particle radius distribution over this bone hierarchy length.
        /// </summary>
        public ObiBone.BonePropertyCurve radius
        {
            get { return _radius; }
            set { _radius = value; bone.UpdateRadius(); }
        }

        /// <summary>  
        /// Mass distribution over this bone hierarchy length.
        /// </summary>
        public ObiBone.BonePropertyCurve mass
        {
            get { return _mass; }
            set { _mass = value; bone.UpdateMasses(); }
        }

        /// <summary>  
        /// Rotational mass distribution over this bone hierarchy length.
        /// </summary>
        public ObiBone.BonePropertyCurve rotationalMass
        {
            get { return _rotationalMass; }
            set { _rotationalMass = value; bone.UpdateMasses(); }
        }

        /// <summary>  
        /// Compliance of this actor's skin constraints.
        /// </summary>
        public ObiBone.BonePropertyCurve skinCompliance
        {
            get { return _skinCompliance; }
            set { _skinCompliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.Skin); }
        }

        /// <summary>  
        /// Compliance of this actor's skin radius
        /// </summary>
        public ObiBone.BonePropertyCurve skinRadius
        {
            get { return _skinRadius; }
            set { _skinRadius = value; bone.SetConstraintsDirty(Oni.ConstraintType.Skin); }
        }

        /// <summary>  
        /// Compliance of this actor's stretch/shear constraints, along their length.
        /// </summary>
        public ObiBone.BonePropertyCurve stretchCompliance
        {
            get { return _stretchCompliance; }
            set { _stretchCompliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.StretchShear); }
        }

        /// <summary>  
        /// Shearing compliance of this actor's stretch/shear constraints, along the first axis orthogonal to their length.
        /// </summary>
        public ObiBone.BonePropertyCurve shear1Compliance
        {
            get { return _shear1Compliance; }
            set { _shear1Compliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.StretchShear); }
        }

        /// <summary>  
        /// Shearing compliance of this actor's stretch/shear constraints, along the second axis orthogonal to their length.
        /// </summary>
        public ObiBone.BonePropertyCurve shear2Compliance
        {
            get { return _shear2Compliance; }
            set { _shear2Compliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.StretchShear); }
        }

        /// <summary>  
        /// Torsional compliance of this actor's bend/twist constraints along their length.
        /// </summary>
        public ObiBone.BonePropertyCurve torsionCompliance
        {
            get { return _torsionCompliance; }
            set { _torsionCompliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Bending compliance of this actor's bend/twist constraints along the first axis orthogonal to their length.
        /// </summary>
        public ObiBone.BonePropertyCurve bend1Compliance
        {
            get { return _bend1Compliance; }
            set { _bend1Compliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Bending compliance of this actor's bend/twist constraints along the second axis orthogonal to their length.
        /// </summary>
        public ObiBone.BonePropertyCurve bend2Compliance
        {
            get { return _bend2Compliance; }
            set { _bend2Compliance = value; bone.SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Threshold for plastic behavior. 
        /// </summary>
        /// Once bending goes above this value, a percentage of the deformation (determined by <see cref="plasticCreep"/>) will be permanently absorbed into the rod's rest shape.
        public ObiBone.BonePropertyCurve plasticYield
        {
            get { return _plasticYield; }
            set { _plasticYield = value; bone.SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Percentage of deformation that gets absorbed into the rest shape per second, once deformation goes above the <see cref="plasticYield"/> threshold.
        /// </summary>
        public ObiBone.BonePropertyCurve plasticCreep
        {
            get { return _plasticCreep; }
            set { _plasticCreep = value; bone.SetConstraintsDirty(Oni.ConstraintType.BendTwist); }
        }

        /// <summary>  
        /// Aerodynamic drag value.
        /// </summary>
        public ObiBone.BonePropertyCurve drag
        {
            get { return _drag; }
            set { _drag = value; bone.SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); }
        }

        /// <summary>  
        /// Aerodynamic lift value.
        /// </summary>
        public ObiBone.BonePropertyCurve lift
        {
            get { return _lift; }
            set { _lift = value; bone.SetConstraintsDirty(Oni.ConstraintType.Aerodynamics); }
        }

        private ObiBone bone;

        public void Awake()
        {
            bone = GetComponentInParent<ObiBone>();
        }

        protected void OnValidate()
        {
            if (bone != null)
            {
                bone.UpdateRadius();
                bone.UpdateMasses();
                bone.SetConstraintsDirty(Oni.ConstraintType.Skin);
                bone.SetConstraintsDirty(Oni.ConstraintType.StretchShear);
                bone.SetConstraintsDirty(Oni.ConstraintType.BendTwist);
                bone.SetConstraintsDirty(Oni.ConstraintType.Aerodynamics);
            }
        }
    }
}
