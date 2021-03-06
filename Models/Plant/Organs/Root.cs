using System;
using System.Collections.Generic;
using System.Text;
using Models.Core;
using Models.PMF.Functions;
using Models.Soils;
using System.Xml.Serialization;
using Models.PMF.Interfaces;
using Models.Soils.Arbitrator;
using APSIM.Shared.Utilities;

namespace Models.PMF.Organs
{
    ///<summary>
    /// The generic root model calculates root growth in terms of rooting depth, biomass accumulation and subsequent root length density.
    ///</summary>
    /// \param InitialDM <b>(Constant)</b> The initial dry weight of root (\f$g mm^{-2}\f$. CHECK).
    /// \param SpecificRootLength <b>(Constant)</b> The length of the specific root 
    ///     (\f$m g^{-1}\f$. CHECK).
    /// \param KNO3 <b>(Constant)</b> Fraction of extractable soil NO3 (\f$K_{NO3}\f$, unitless).  
    /// \param KNH4 <b>(Constant)</b> Fraction of extractable soil NH4 (\f$K_{NH4}\f$, unitless).  
    /// \param NitrogenDemandSwitch <b>(IFunction)</b> Whether to switch on nitrogen demand 
    ///     when nitrogen deficit is calculated (0 or 1, unitless).
    /// \param RootFrontVelocity <b>(IFunction)</b> The daily growth speed of root depth 
    ///     (\f$mm d^{-1}\f$. CHECK).
    /// \param PartitionFraction <b>(IFunction)</b> The fraction of biomass partitioning 
    ///     into root (0-1, unitless).
    /// \param KLModifier <b>(IFunction)</b> The modifier for KL factor which is defined as 
    ///     the fraction of available water able to be extracted per day, and empirically 
    ///     derived incorporating both plant and soil factors which limit rate of water 
    ///     update (0-1, unitless).
    /// \param TemperatureEffect <b>(IFunction)</b> 
    ///     The temperature effects on root depth growth (0-1, unitless).
    /// \param MaximumNConc <b>(IFunction)</b> 
    ///     Maximum nitrogen concentration (\f$g m^{-2}\f$. CHECK).
    /// \param MinimumNConc <b>(IFunction)</b> 
    ///     Minimum nitrogen concentration (\f$g m^{-2}\f$. CHECK).
    /// \param MaxDailyNUptake <b>(IFunction)</b> 
    ///     Maximum daily nitrogen update (\f$kg ha^{-1}\f$. CHECK).
    /// 
    /// \param SenescenceRate <b>(IFunction, Optional)</b> The daily senescence rate of 
    ///     root length (0-1, unitless).
    ///     
    /// \retval Length Total root length (mm).
    /// \retval Depth Root depth (mm).
    /// 
    ///<remarks>
    /// 
    /// Potential root growth 
    /// ------------------------
    ///  
    /// Actual root growth
    /// ------------------------
    /// 
    /// Nitrogen deficit 
    /// ------------------------
    /// 
    ///</remarks>
    [Serializable]
    [Description("Root Class")]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class Root : BaseOrgan, BelowGround
    {
        #region Links
        /// <summary>The arbitrator</summary>
        [Link]
        OrganArbitrator Arbitrator = null;

        /// <summary>The soil</summary>
        [Link]
        Soils.Soil Soil = null;

        /// <summary>Link to the KNO3 link</summary>
        [Link]
        LinearInterpolationFunction KNO3 = null;
        /// <summary>
        /// Soil water factor for N Uptake
        /// </summary>
        [Link]
        LinearInterpolationFunction NUptakeSWFactor = null;
        /// <summary>Link to the KNH4 link</summary>
        [Link]
        LinearInterpolationFunction KNH4 = null;
        #endregion

        #region Parameters
        /// <summary>Gets or sets the initial dm.</summary>
        /// <value>The initial dm.</value>
        public double InitialDM { get; set; }
        /// <summary>Gets or sets the length of the specific root.</summary>
        /// <value>The length of the specific root.</value>
        public double SpecificRootLength { get; set; }
        
        /// <summary>The nitrogen demand switch</summary>
        [Link]
        IFunction NitrogenDemandSwitch = null;
        /// <summary>The senescence rate</summary>
        [Link(IsOptional = true)]
        [Units("/d")]
        IFunction SenescenceRate = null;
        /// <summary>The temperature effect</summary>
        [Link(IsOptional = true)]
        [Units("0-1")]
        IFunction TemperatureEffect = null;
        /// <summary>The root front velocity</summary>
        [Link]
        [Units("mm/d")]
        IFunction RootFrontVelocity = null;
        /// <summary>The partition fraction</summary>
        [Link(IsOptional = true)]
        [Units("0-1")]
        IFunction PartitionFraction = null;
        /// <summary>The maximum n conc</summary>
        [Link(IsOptional = true)]
        [Units("g/g")]
        IFunction MaximumNConc = null;
        /// <summary>The maximum daily n uptake</summary>
        [Link]
        [Units("kg N/ha")]
        IFunction MaxDailyNUptake = null;
        /// <summary>The minimum n conc</summary>
        [Link(IsOptional = true)]
        [Units("g/g")]
        IFunction MinimumNConc = null;
        /// <summary>The kl modifier</summary>
        [Link]
        [Units("0-1")]
        IFunction KLModifier = null;
        /// <summary>The Maximum Root Depth</summary>
        [Link(IsOptional = true)]
        [Units("0-1")]
        IFunction MaximumRootDepth = null;

        #endregion

        #region States
        /// <summary>The kgha2gsm</summary>
        private const double kgha2gsm = 0.1;
        /// <summary>The uptake</summary>
        private double[] Uptake = null;
        /// <summary>The delta n h4</summary>
        private double[] DeltaNH4;
        /// <summary>The delta n o3</summary>
        private double[] DeltaNO3;
        /// <summary>
        /// Holds actual DM allocations to use in allocating N to structural and Non-Structural pools
        /// </summary>
        [XmlIgnore]
        [Units("g/2")]
        public double[] DMAllocated { get; set; }
        /// <summary>
        /// Demand for structural N, set when Ndemand is called and used again in N allocation
        /// </summary>
        [XmlIgnore]
        [Units("g/2")]
        public double[] StructuralNDemand { get; set; }
        /// <summary>
        /// Demand for Non-structural N, set when Ndemand is called and used again in N allocation
        /// </summary>
        [XmlIgnore]
        [Units("g/m2")]
        public double[] NonStructuralNDemand { get; set; }
        /// <summary>The _ senescence rate</summary>
        private double _SenescenceRate = 0;
        /// <summary>The Nuptake</summary>
        private double[] NitUptake = null;

        /// <summary>Gets or sets the layer live.</summary>
        /// <value>The layer live.</value>
        [XmlIgnore]
        public Biomass[] LayerLive { get; set; }
        /// <summary>Gets or sets the layer dead.</summary>
        /// <value>The layer dead.</value>
        [XmlIgnore]
        public Biomass[] LayerDead { get; set; }
        /// <summary>Gets or sets the length.</summary>
        /// <value>The length.</value>
        [XmlIgnore]
        public double Length { get; set; }

        /// <summary>Gets or sets the depth.</summary>
        /// <value>The depth.</value>
        [XmlIgnore]
        [Units("mm")]
        public double Depth { get; set; }

        /// <summary>Gets depth or the mid point of the cuttent layer under examination</summary>
        /// <value>The depth.</value>
        [XmlIgnore]
        [Units("mm")]
        public double LayerMidPointDepth { get; set; }

        /// <summary>Clears this instance.</summary>
        protected override void Clear()
        {
            base.Clear();
            Uptake = null;
            NitUptake = null;
            DeltaNH4 = null;
            DeltaNO3 = null;
            _SenescenceRate = 0;
            Length = 0;
            Depth = 0;

            if (LayerLive == null || LayerLive.Length == 0)
            {
                LayerLive = new Biomass[Soil.Thickness.Length];
                LayerDead = new Biomass[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                {
                    LayerLive[i] = new Biomass();
                    LayerDead[i] = new Biomass();
                }
            }
            else
            {
                for (int i = 0; i < Soil.Thickness.Length; i++)
                {
                    LayerLive[i].Clear();
                    LayerDead[i].Clear();
                }
            }


            DeltaNO3 = new double[Soil.Thickness.Length];
            DeltaNH4 = new double[Soil.Thickness.Length];
        }

        #endregion
        
        #region Class Properties
        /// <summary>Gets a value indicating whether this instance is growing.</summary>
        /// <value>
        /// <c>true</c> if this instance is growing; otherwise, <c>false</c>.
        /// </value>
        private bool isGrowing { get { return (Plant.IsAlive && Plant.SowingData.Depth < this.Depth); } }

        /// <summary>The soil crop</summary>
        private SoilCrop soilCrop;

        /// <summary>Gets the l ldep.</summary>
        /// <value>The l ldep.</value>
        [Units("mm")]
        double[] LLdep
        {
            get
            {
                double[] value = new double[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                    value[i] = soilCrop.LL[i] * Soil.Thickness[i];
                return value;
            }
        }

        /// <summary>Gets the length density.</summary>
        /// <value>The length density.</value>
        [Units("??mm/mm3")]
        public double[] LengthDensity
        {
            get
            {
                double[] value = new double[Soil.Thickness.Length];
                for (int i = 0; i < Soil.Thickness.Length; i++)
                    value[i] = LayerLive[i].Wt * SpecificRootLength / 1000000 / Soil.Thickness[i];
                return value;
            }
        }

        /// <summary>Gets the RLV.</summary>
        /// <value>The RLV.</value>
        [Units("??km/mm3")]
        double[] rlv
        {
            get
            {
                return LengthDensity;
            }
        }

        ///<Summary>Sum Non-Structural N demand for all layers</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalNonStructuralNDemand { get; set; }
        ///<Summary>Sum Structural N demand for all layers</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalStructuralNDemand { get; set; }
        ///<Summary>Sum N demand for all layers</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalNDemand { get; set; }
        ///<Summary>Superfloruis docummentation added to get solution compilling</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalNAllocated { get; set; }
        ///<Summary>Superfloruis docummentation added to get solution compilling</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalDMDemand { get; set; }
        ///<Summary>Superfloruis docummentation added to get solution compilling</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalDMAllocated { get; set; }
        ///<Summary>The amount of N taken up after arbitration</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double NTakenUp { get; set; }

        #endregion

        #region Functions

        /// <summary>
        /// Gets or sets the nuptake supply.
        /// </summary>
        public double NuptakeSupply { get; set; }

        /// <summary>Event from sequencer telling us to do our potential growth.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoPotentialPlantGrowth")]
        private void OnDoPotentialPlantGrowth(object sender, EventArgs e)
        {
            if (Plant.IsEmerged)
            {
                _SenescenceRate = 0;
                if (SenescenceRate != null) //Default of zero means no senescence
                    _SenescenceRate = SenescenceRate.Value;
           
                /*  if (Live.Wt == 0)
                  {
                      //determine how many layers to put initial DM into.
                      Depth = Plant.SowingData.Depth;
                      double AccumulatedDepth = 0;
                      double InitialLayers = 0;
                      for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                      {
                          if (AccumulatedDepth < Depth)
                              InitialLayers += 1;
                          AccumulatedDepth += Soil.SoilWater.Thickness[layer];
                      }
                      for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                      {
                          if (layer <= InitialLayers - 1)
                          {
                              //dirstibute root biomass evently through root depth
                              LayerLive[layer].StructuralWt = InitialDM / InitialLayers * Plant.Population;
                              LayerLive[layer].StructuralN = InitialDM / InitialLayers * MaxNconc * Plant.Population;
                          }
                      }
               
                  }
                  */
                Length = 0;
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                    Length += LengthDensity[layer];
            }
        }

        /// <summary>Does the nutrient allocations.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoActualPlantGrowth")]
        private void OnDoActualPlantGrowth(object sender, EventArgs e)
        {

            if (Plant.IsAlive)
            {
                // Do Root Front Advance
                int RootLayer = LayerIndex(Depth);
                double TEM = (TemperatureEffect == null) ? 1 : TemperatureEffect.Value;

                Depth = Depth + RootFrontVelocity.Value * soilCrop.XF[RootLayer] * TEM;

                //Limit root depth for impeded layers
                double MaxDepth = 0;
                for (int i = 0; i < Soil.Thickness.Length; i++)
                    if (soilCrop.XF[i] > 0)
                        MaxDepth += Soil.Thickness[i];
                //Limit root depth for the crop specific maximum depth
                if (MaximumRootDepth != null)
                    MaxDepth = Math.Min(MaximumRootDepth.Value, MaxDepth);

                Depth = Math.Min(Depth, MaxDepth);

                // Do Root Senescence
                FOMLayerLayerType[] FOMLayers = new FOMLayerLayerType[Soil.Thickness.Length];

                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    double DM = LayerLive[layer].Wt * _SenescenceRate * 10.0;
                    double N = LayerLive[layer].StructuralN * _SenescenceRate * 10.0;
                    LayerLive[layer].StructuralWt *= (1.0 - _SenescenceRate);
                    LayerLive[layer].NonStructuralWt *= (1.0 - _SenescenceRate);
                    LayerLive[layer].StructuralN *= (1.0 - _SenescenceRate);
                    LayerLive[layer].NonStructuralN *= (1.0 - _SenescenceRate);



                    FOMType fom = new FOMType();
                    fom.amount = (float)DM;
                    fom.N = (float)N;
                    fom.C = (float)(0.40 * DM);
                    fom.P = 0;
                    fom.AshAlk = 0;

                    FOMLayerLayerType Layer = new FOMLayerLayerType();
                    Layer.FOM = fom;
                    Layer.CNR = 0;
                    Layer.LabileP = 0;

                    FOMLayers[layer] = Layer;
                }
                FOMLayerType FomLayer = new FOMLayerType();
                FomLayer.Type = Plant.CropType;
                FomLayer.Layer = FOMLayers;
                IncorpFOM.Invoke(FomLayer);
            }
        }

        /// <summary>Does the water uptake.</summary>
        /// <param name="Amount">The amount.</param>
        public override void DoWaterUptake(double[] Amount)
        {
            // Send the delta water back to SoilWat that we're going to uptake.
            WaterChangedType WaterUptake = new WaterChangedType();
            WaterUptake.DeltaWater = MathUtilities.Multiply_Value(Amount, -1.0);

            Uptake = WaterUptake.DeltaWater;
            if (WaterChanged != null)
                WaterChanged.Invoke(WaterUptake);
        }

        /// <summary>Does the Nitrogen uptake.</summary>
        /// <param name="NO3NAmount">The NO3NAmount.</param>
        /// <param name="NH4NAmount">The NH4NAmount.</param>
        public override void DoNitrogenUptake(double[] NO3NAmount, double[] NH4NAmount)
        {
            // Send the delta water back to SoilN that we're going to uptake.
            NitrogenChangedType NitrogenUptake = new NitrogenChangedType();
            NitrogenUptake.DeltaNO3 = MathUtilities.Multiply_Value(NO3NAmount, -1.0);
            NitrogenUptake.DeltaNH4 = MathUtilities.Multiply_Value(NH4NAmount, -1.0);

            NitUptake = MathUtilities.Add(NitrogenUptake.DeltaNO3, NitrogenUptake.DeltaNH4);
            if (NitrogenChanged != null)
                NitrogenChanged.Invoke(NitrogenUptake);
        }
        /// <summary>Layers the index.</summary>
        /// <param name="depth">The depth.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Depth deeper than bottom of soil profile</exception>
        private int LayerIndex(double depth)
        {
            double CumDepth = 0;
            for (int i = 0; i < Soil.Thickness.Length; i++)
            {
                CumDepth = CumDepth + Soil.Thickness[i];
                if (CumDepth >= depth) { return i; }
            }
            throw new Exception("Depth deeper than bottom of soil profile");
        }
        /// <summary>Roots the proportion.</summary>
        /// <param name="layer">The layer.</param>
        /// <param name="root_depth">The root_depth.</param>
        /// <returns></returns>
        private double RootProportion(int layer, double root_depth)
        {
            double depth_to_layer_bottom = 0;   // depth to bottom of layer (mm)
            double depth_to_layer_top = 0;      // depth to top of layer (mm)
            double depth_to_root = 0;           // depth to root in layer (mm)
            double depth_of_root_in_layer = 0;  // depth of root within layer (mm)
            // Implementation Section ----------------------------------
            for (int i = 0; i <= layer; i++)
                depth_to_layer_bottom += Soil.Thickness[i];
            depth_to_layer_top = depth_to_layer_bottom - Soil.Thickness[layer];
            depth_to_root = Math.Min(depth_to_layer_bottom, root_depth);
            depth_of_root_in_layer = Math.Max(0.0, depth_to_root - depth_to_layer_top);

            return depth_of_root_in_layer / Soil.Thickness[layer];
        }
        /// <summary>Soils the n supply.</summary>
        /// <param name="NO3Supply">The n o3 supply.</param>
        /// <param name="NH4Supply">The n h4 supply.</param>
        private void SoilNSupply(double[] NO3Supply, double[] NH4Supply)
        {
            double[] no3ppm = new double[Soil.Thickness.Length];
            double[] nh4ppm = new double[Soil.Thickness.Length];

            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                if (LayerLive[layer].Wt > 0)
                {
                    double kno3 = KNO3.ValueForX(LengthDensity[layer]);
                    double knh4 = KNH4.ValueForX(LengthDensity[layer]);
                    double RWC = 0;
                    RWC = (Soil.Water[layer] - Soil.SoilWater.LL15mm[layer]) / (Soil.SoilWater.DULmm[layer] - Soil.SoilWater.LL15mm[layer]);
                    RWC = Math.Max(0.0, Math.Min(RWC, 1.0));
                    double SWAF = NUptakeSWFactor.ValueForX(RWC);
                    no3ppm[layer] = Soil.NO3N[layer] * (100.0 / (Soil.BD[layer] * Soil.Thickness[layer]));
                    NO3Supply[layer] = Soil.NO3N[layer] * kno3 * no3ppm[layer] * SWAF;
                    nh4ppm[layer] = Soil.NH4N[layer] * (100.0 / (Soil.BD[layer] * Soil.Thickness[layer]));
                    NH4Supply[layer] = Soil.NH4N[layer] * knh4 * nh4ppm[layer] * SWAF;
                }
                else
                {
                    NO3Supply[layer] = 0;
                    NH4Supply[layer] = 0;
                }
            }
        }

        /// <summary>Called when [simulation commencing].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <exception cref="ApsimXException">Cannot find a soil crop parameterisation for  + Name</exception>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            soilCrop = this.Soil.Crop(this.Plant.Name) as SoilCrop;
            if (soilCrop == null)
                throw new ApsimXException(this, "Cannot find a soil crop parameterisation for " + Name);
            Clear();
        }

        /// <summary>Called when crop is ending</summary>
        public override void DoPlantEnding()
        {
            
                FOMLayerLayerType[] FOMLayers = new FOMLayerLayerType[Soil.Thickness.Length];

                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    double DM = (LayerLive[layer].Wt + LayerDead[layer].Wt) * 10.0;
                    double N = (LayerLive[layer].N + LayerDead[layer].N) * 10.0;

                    FOMType fom = new FOMType();
                    fom.amount = (float)DM;
                    fom.N = (float)N;
                    fom.C = (float)(0.40 * DM);
                    fom.P = 0;
                    fom.AshAlk = 0;

                    FOMLayerLayerType Layer = new FOMLayerLayerType();
                    Layer.FOM = fom;
                    Layer.CNR = 0;
                    Layer.LabileP = 0;

                    FOMLayers[layer] = Layer;
                }
                FOMLayerType FomLayer = new FOMLayerType();
                FomLayer.Type = Plant.CropType;
                FomLayer.Layer = FOMLayers;
                IncorpFOM.Invoke(FomLayer);

                Clear();
       }

        /// <summary>Called when crop is ending</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="data">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantSowing")]
        private void OnPlantSowing(object sender, SowPlant2Type data)
        {
            if (data.Plant == Plant)
            {
                Depth = Plant.SowingData.Depth;
                double AccumulatedDepth = 0;
                double InitialLayers = 0;
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (AccumulatedDepth < Depth)
                        InitialLayers += 1;
                    AccumulatedDepth += Soil.Thickness[layer];
                }
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (layer <= InitialLayers - 1)
                    {
                        //dirstibute root biomass evently through root depth
                        LayerLive[layer].StructuralWt = InitialDM / InitialLayers * Plant.Population;
                        LayerLive[layer].StructuralN = InitialDM / InitialLayers * MaxNconc * Plant.Population;
                    }
                }
            }
        }
        #endregion

        #region Arbitrator method calls
        /// <summary>Gets or sets the dm demand.</summary>
        /// <value>The dm demand.</value>
        public override BiomassPoolType DMDemand
        {
            get
            {
                double Demand = 0;
                if ((isGrowing)&&(PartitionFraction != null))
                    Demand = Arbitrator.DMSupply * PartitionFraction.Value;
                TotalDMDemand = Demand;//  The is not really necessary as total demand is always not calculated on a layer basis so doesn't need summing.  However it may some day
                return new BiomassPoolType { Structural = Demand };
            }
        }

        /// <summary>Sets the dm potential allocation.</summary>
        /// <value>The dm potential allocation.</value>
        /// <exception cref="System.Exception">
        /// Invalid allocation of potential DM in + Name
        /// or
        /// Error trying to partition potential root biomass
        /// </exception>
        public override BiomassPoolType DMPotentialAllocation
        {
            set
            {
                if (Uptake == null)
                    throw new Exception("No water and N uptakes supplied to root. Is Soil Arbitrator included in the simulation?");
           
                if (Depth <= 0)
                    return; //cannot allocate growth where no length

                if (DMDemand.Structural == 0)
                    if (value.Structural < 0.000000000001) { }//All OK
                    else
                        throw new Exception("Invalid allocation of potential DM in" + Name);
                // Calculate Root Activity Values for water and nitrogen
                double[] RAw = new double[Soil.Thickness.Length];
                double[] RAn = new double[Soil.Thickness.Length];
                double TotalRAw = 0;
                double TotalRAn = 0; ;

                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (layer <= LayerIndex(Depth))
                        if (LayerLive[layer].Wt > 0)
                        {
                            RAw[layer] = Uptake[layer] / LayerLive[layer].Wt
                                       * Soil.Thickness[layer]
                                       * RootProportion(layer, Depth);
                            RAw[layer] = Math.Max(RAw[layer], 1e-20);  // Make sure small numbers to avoid lack of info for partitioning

                            RAn[layer] = (DeltaNO3[layer] + DeltaNH4[layer]) / LayerLive[layer].Wt
                                           * Soil.Thickness[layer]
                                           * RootProportion(layer, Depth);
                            RAn[layer] = Math.Max(RAw[layer], 1e-10);  // Make sure small numbers to avoid lack of info for partitioning
                        }
                        else if (layer > 0)
                        {
                            RAw[layer] = RAw[layer - 1];
                            RAn[layer] = RAn[layer - 1];
                        }
                        else
                        {
                            RAw[layer] = 0;
                            RAn[layer] = 0;
                        }
                    TotalRAw += RAw[layer];
                    TotalRAn += RAn[layer];
                }
                double allocated = 0;
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (TotalRAw > 0)

                        LayerLive[layer].PotentialDMAllocation = value.Structural * RAw[layer] / TotalRAw;
                    else if (value.Structural > 0)
                        throw new Exception("Error trying to partition potential root biomass");
                    allocated += (TotalRAw > 0) ? value.Structural * RAw[layer] / TotalRAw : 0;
                }
            }
        }
        /// <summary>Sets the dm allocation.</summary>
        /// <value>The dm allocation.</value>
        /// <exception cref="System.Exception">Error trying to partition root biomass</exception>
        public override BiomassAllocationType DMAllocation
        {
            set
            {
                TotalDMAllocated = value.Structural;
                DMAllocated = new double[Soil.Thickness.Length];
            
                // Calculate Root Activity Values for water and nitrogen
                double[] RAw = new double[Soil.Thickness.Length];
                double[] RAn = new double[Soil.Thickness.Length];
                double TotalRAw = 0;
                double TotalRAn = 0;

                if (Depth <= 0)
                    return; // cannot do anything with no depth
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (layer <= LayerIndex(Depth))
                        if (LayerLive[layer].Wt > 0)
                        {
                            RAw[layer] = Uptake[layer] / LayerLive[layer].Wt
                                       * Soil.Thickness[layer]
                                       * RootProportion(layer, Depth);
                            RAw[layer] = Math.Max(RAw[layer], 1e-20);  // Make sure small numbers to avoid lack of info for partitioning

                            RAn[layer] = (DeltaNO3[layer] + DeltaNH4[layer]) / LayerLive[layer].Wt
                                       * Soil.Thickness[layer]
                                       * RootProportion(layer, Depth);
                            RAn[layer] = Math.Max(RAw[layer], 1e-10);  // Make sure small numbers to avoid lack of info for partitioning

                        }
                        else if (layer > 0)
                        {
                            RAw[layer] = RAw[layer - 1];
                            RAn[layer] = RAn[layer - 1];
                        }
                        else
                        {
                            RAw[layer] = 0;
                            RAn[layer] = 0;
                        }
                    TotalRAw += RAw[layer];
                    TotalRAn += RAn[layer];
                }
                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {
                    if (TotalRAw > 0)
                    {
                        LayerLive[layer].StructuralWt += value.Structural * RAw[layer] / TotalRAw;
                        DMAllocated[layer] += value.Structural * RAw[layer] / TotalRAw;
                    }
                    else if (value.Structural > 0)
                        throw new Exception("Error trying to partition root biomass");
                        
                }
            }
        }

        /// <summary>Gets or sets the n demand.</summary>
        /// <value>The n demand.</value>
        [Units("g/m2")]
        public override BiomassPoolType NDemand
        {
            get
            {
                StructuralNDemand = new double[Soil.Thickness.Length];
                NonStructuralNDemand = new double[Soil.Thickness.Length];
            
                //Calculate N demand based on amount of N needed to bring root N content in each layer up to maximum
                double _NitrogenDemandSwitch = 1;
                if (NitrogenDemandSwitch != null) //Default of 1 means demand is always truned on!!!!
                    _NitrogenDemandSwitch = NitrogenDemandSwitch.Value;
                int i = -1;
                foreach (Biomass Layer in LayerLive)
                {
                    i += 1;
                    StructuralNDemand[i] = Layer.PotentialDMAllocation * MinNconc *  _NitrogenDemandSwitch;
                    double NDeficit = Math.Max(0.0, MaxNconc * (Layer.Wt + Layer.PotentialDMAllocation) - (Layer.N + StructuralNDemand[i]));
                    NonStructuralNDemand[i] = Math.Max(0, NDeficit - StructuralNDemand[i]) * _NitrogenDemandSwitch;
                }
                TotalNonStructuralNDemand = MathUtilities.Sum(NonStructuralNDemand);
                TotalStructuralNDemand = MathUtilities.Sum(StructuralNDemand);
                TotalNDemand = TotalNonStructuralNDemand + TotalStructuralNDemand;
                return new BiomassPoolType { Structural = TotalStructuralNDemand, NonStructural = TotalNonStructuralNDemand };
            }
        }

        /// <summary>Gets or sets the n supply.</summary>
        /// <value>The n supply.</value>
        public override BiomassSupplyType NSupply
        {
            get
            {
                if (Soil.Thickness != null)
                {
                    double[] no3supply = new double[Soil.Thickness.Length];
                    double[] nh4supply = new double[Soil.Thickness.Length];
                    SoilNSupply(no3supply, nh4supply);
                    double NSupply = (Math.Min(MathUtilities.Sum(no3supply), MaxDailyNUptake.Value) + Math.Min(MathUtilities.Sum(nh4supply), MaxDailyNUptake.Value)) * kgha2gsm;
                    NuptakeSupply = NSupply;
                    return new BiomassSupplyType { Uptake = NSupply };
                    
                }
                else
                    return new BiomassSupplyType();
            }
        }

        /// <summary>Gets the nitrogne supply.</summary>
        /// <value>The water supply.</value>
        public override double[] NO3NSupply(List<ZoneWaterAndN> zones)
        {
             
            // Model can only handle one root zone at present
            ZoneWaterAndN MyZone = new ZoneWaterAndN();
            Zone ParentZone = Apsim.Parent(this, typeof(Zone)) as Zone;
            foreach (ZoneWaterAndN Z in zones)
                if (Z.Name == ParentZone.Name)
                    MyZone = Z;


            double[] NO3 = MyZone.NO3N;

            double[] NO3Supply = new double[Soil.Thickness.Length];

            double[] no3ppm = new double[Soil.Thickness.Length];

            double NO3uptake = 0;
           
            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                if (LayerLive[layer].Wt > 0)
                {
                    double RWC = 0;
					RWC = (Soil.Water[layer] - Soil.SoilWater.LL15mm[layer]) / (Soil.SoilWater.DULmm[layer] - Soil.SoilWater.LL15mm[layer]);
                    RWC = Math.Max(0.0, Math.Min(RWC, 1.0));
                    double kno3 = KNO3.ValueForX(LengthDensity[layer]);
                    double SWAF = NUptakeSWFactor.ValueForX(RWC);
                    no3ppm[layer] = NO3[layer] * (100.0 / (Soil.BD[layer] * Soil.Thickness[layer]));
                    NO3Supply[layer] = Math.Min(NO3[layer] * kno3 * no3ppm[layer] * SWAF, (MaxDailyNUptake.Value - NO3uptake));
                    NO3uptake += NO3Supply[layer];
                }
                else
                {
                    NO3Supply[layer] = 0;
                }
            }

            return NO3Supply;
        }
        /// <summary>Gets the nitrogne supply.</summary>
        /// <value>The water supply.</value>
        public override double[] NH4NSupply(List<ZoneWaterAndN> zones)
        {
            // Model can only handle one root zone at present
            ZoneWaterAndN MyZone = new ZoneWaterAndN();
            Zone ParentZone = Apsim.Parent(this, typeof(Zone)) as Zone;
            foreach (ZoneWaterAndN Z in zones)
                if (Z.Name == ParentZone.Name)
                    MyZone = Z;

            double[] NH4 = MyZone.NH4N;

            double[] NH4Supply = new double[Soil.Thickness.Length];

            double[] NH4ppm = new double[Soil.Thickness.Length];

            double NH4uptake = 0;

            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                if (LayerLive[layer].Wt > 0)
                {
                    double RWC = 0;
                    RWC = (Soil.Water[layer] - Soil.SoilWater.LL15mm[layer]) / (Soil.SoilWater.DULmm[layer] - Soil.SoilWater.LL15mm[layer]);
                    RWC = Math.Max(0.0, Math.Min(RWC, 1.0));
                    double knh4 = KNH4.ValueForX(LengthDensity[layer]);
                    double SWAF = NUptakeSWFactor.ValueForX(RWC);
                    NH4ppm[layer] = NH4Supply[layer] * (100.0 / (Soil.BD[layer] * Soil.Thickness[layer]));
                    NH4Supply[layer] = Math.Min(NH4[layer] * knh4 * NH4ppm[layer] * SWAF, (MaxDailyNUptake.Value - NH4uptake));
                    NH4uptake += NH4Supply[layer]; 
                }
                else
                {
                    NH4Supply[layer] = 0;
                }
            }

            return NH4Supply;
        }
        /// <summary>Sets the n allocation.</summary>
        /// <value>The n allocation.</value>
        /// <exception cref="System.Exception">
        /// Cannot Allocate N to roots in layers when demand is zero
        /// or
        /// Error in N Allocation:  + Name
        /// or
        /// Request for N uptake exceeds soil N supply
        /// </exception>
        public override BiomassAllocationType NAllocation
        {
            set
            {
                NTakenUp = value.Uptake;
                TotalNAllocated = value.Structural + value.NonStructural;
                double surpluss = TotalNAllocated - TotalNDemand;
                if (surpluss > 0.000000001)
                     { throw new Exception("N Allocation to roots exceeds Demand"); }
                
                double NAllocated = 0;
                int i = -1;
                foreach (Biomass Layer in LayerLive)
                {
                    i += 1;
                    if (TotalStructuralNDemand > 0)
                    {
                        double StructFrac = StructuralNDemand[i] / TotalStructuralNDemand;
                        Layer.StructuralN += value.Structural * StructFrac;
                        NAllocated += value.Structural * StructFrac;
                    }
                    if (TotalNonStructuralNDemand > 0)
                    {
                        double NonStructFrac = NonStructuralNDemand[i] / TotalNonStructuralNDemand;
                        Layer.NonStructuralN += value.NonStructural * NonStructFrac;
                        NAllocated += value.NonStructural * NonStructFrac;
                    }
                }
                if (!MathUtilities.FloatsAreEqual(NAllocated - TotalNAllocated, 0.0))
                {
                    throw new Exception("Error in N Allocation: " + Name);
                }
            }
        }
        /// <summary>Gets or sets the maximum nconc.</summary>
        /// <value>The maximum nconc.  Has a default of 0.01</value>
        public override double MaxNconc
        {
            get
            {
                if (MaximumNConc != null)
                    return MaximumNConc.Value;
                else
                    return 0.01; 
            }
        }
        /// <summary>Gets or sets the minimum nconc.</summary>
        /// <value>The minimum nconc. Has a default of 0.01</value>
        public override double MinNconc
        {
            get
            {
                if (MinimumNConc != null)
                    return MinimumNConc.Value;
                else
                    return 0.01;
            }
        }


        /// <summary>Gets or sets the water supply.</summary>
        /// <value>The water supply.</value>
        public override double[] WaterSupply(List<ZoneWaterAndN> zones)
        {
            // Model can only handle one root zone at present
            ZoneWaterAndN MyZone = new ZoneWaterAndN();
            Zone ParentZone = Apsim.Parent(this, typeof(Zone)) as Zone;
            foreach (ZoneWaterAndN Z in zones)
                if (Z.Name == ParentZone.Name)
                    MyZone = Z;

            double[] SW = MyZone.Water;
            double[] supply = new double[Soil.Thickness.Length];

            double depth_to_layer_bottom = 0;   // depth to bottom of layer (mm)
            double depth_to_layer_top = 0;      // depth to top of layer (mm)

            for (int layer = 0; layer < Soil.Thickness.Length; layer++)
            {
                depth_to_layer_bottom += Soil.Thickness[layer];
                depth_to_layer_top = depth_to_layer_bottom - Soil.Thickness[layer];
                LayerMidPointDepth = (depth_to_layer_bottom + depth_to_layer_top) / 2;

                if (layer <= LayerIndex(Depth))
                    supply[layer] = Math.Max(0.0, soilCrop.KL[layer] * KLModifier.Value *
                        (SW[layer] - soilCrop.LL[layer] * Soil.Thickness[layer]) * RootProportion(layer, Depth));
                else
                    supply[layer] = 0;
            }

            return supply;
        }

        /// <summary>Gets or sets the water uptake.</summary>
        /// <value>The water uptake.</value>
        [Units("mm")]
        public override double WaterUptake
        {
            get { return Uptake == null ? 0.0 : -MathUtilities.Sum(Uptake); }
        }
        
        /// <summary>Gets or sets the water uptake.</summary>
        /// <value>The water uptake.</value>
        [Units("kg/ha")]
        public override double NUptake
        {
            get {return NitUptake == null ? 0.0 : -MathUtilities.Sum(NitUptake);}
        }
        #endregion

        #region Event handlers


        /// <summary>Called when [water uptakes calculated].</summary>
        /// <param name="SoilWater">The soil water.</param>
        [EventSubscribe("WaterUptakesCalculated")]
        private void OnWaterUptakesCalculated(WaterUptakesCalculatedType SoilWater)
        {
        
            // Gets the water uptake for each layer as calculated by an external module (SWIM)

            Uptake = new double[Soil.Thickness.Length];

            for (int i = 0; i != SoilWater.Uptakes.Length; i++)
            {
                string UName = SoilWater.Uptakes[i].Name;
                if (UName == Plant.Name)
                {
                    int length = SoilWater.Uptakes[i].Amount.Length;
                    for (int layer = 0; layer < length; layer++)
                    {
                        Uptake[layer] = -(float)SoilWater.Uptakes[i].Amount[layer];
                    }
                }
            }
        }

        /// <summary>Occurs when [incorp fom].</summary>
        public event FOMLayerDelegate IncorpFOM;

        /// <summary>Occurs when [nitrogen changed].</summary>
        public event NitrogenChangedDelegate NitrogenChanged;

        /// <summary>Occurs when [nitrogen changed].</summary>
        public event WaterChangedDelegate WaterChanged;
        #endregion

        #region Biomass Removal
        /// <summary>Removes biomass from root layers when harvest, graze or cut events are called.</summary>
        public override void DoRemoveBiomass(OrganBiomassRemovalType value)
        {
            double RemainFrac = 1 - (value.FractionToResidue + value.FractionRemoved);
            if (RemainFrac < 0)
                throw new Exception("The sum of FractionToResidue and FractionRemoved sent with your " + "Place holder for event sender" + " is greater than 1.  Had this execption not triggered you would be removing more biomass from " + Name + " than there is to remove");
            if (RemainFrac < 1)
            {
                FOMLayerLayerType[] FOMLayers = new FOMLayerLayerType[Soil.Thickness.Length];

                for (int layer = 0; layer < Soil.Thickness.Length; layer++)
                {

                    double DM = (LayerLive[layer].Wt + LayerDead[layer].Wt) * 10.0;
                    double N = (LayerLive[layer].N + LayerDead[layer].N) * 10.0;

                    FOMType fom = new FOMType();
                    fom.amount = (float)DM;
                    fom.N = (float)N;
                    fom.C = (float)(0.40 * DM);
                    fom.P = 0;
                    fom.AshAlk = 0;

                    FOMLayerLayerType Layer = new FOMLayerLayerType();
                    Layer.FOM = fom;
                    Layer.CNR = 0;
                    Layer.LabileP = 0;

                    FOMLayers[layer] = Layer;

                    if (LayerLive[layer].StructuralWt > 0)
                        LayerLive[layer].StructuralWt *= RemainFrac;
                    if (LayerLive[layer].NonStructuralWt > 0)
                        LayerLive[layer].NonStructuralWt *= RemainFrac;
                    if (LayerLive[layer].StructuralN > 0)
                        LayerLive[layer].StructuralN *= RemainFrac;
                    if (LayerLive[layer].NonStructuralN > 0)
                        LayerLive[layer].NonStructuralN *= RemainFrac;

                }
                Summary.WriteMessage(this, "Harvesting " + Name + " from " + Plant.Name + " removing " + value.FractionRemoved * 100 + "% and returning " + value.FractionToResidue * 100 + "% to the soil organic matter");
                FOMLayerType FomLayer = new FOMLayerType();
                FomLayer.Type = Plant.CropType;
                FomLayer.Layer = FOMLayers;
                IncorpFOM.Invoke(FomLayer);
            }
        }
        #endregion

        /// <summary>Writes documentation for this function by adding to the list of documentation tags.</summary>
        /// <param name="tags">The list of tags to add to.</param>
        /// <param name="headingLevel">The level (e.g. H2) of the headings.</param>
        /// <param name="indent">The level of indentation 1, 2, 3 etc.</param>
        public override void Document(List<AutoDocumentation.ITag> tags, int headingLevel, int indent)
        {
            // add a heading.
            Name = this.Name;
            tags.Add(new AutoDocumentation.Heading(Name, headingLevel));

            tags.Add(new AutoDocumentation.Paragraph(Name + " is parameterised using the PMF Root class which provides the core functions of taking up water and nutrients from the soil.  It is parameterised as follows.", indent));

            // write memos.
            foreach (IModel memo in Apsim.Children(this, typeof(Memo)))
                memo.Document(tags, -1, indent);

            //Describe root growth
            tags.Add(new AutoDocumentation.Heading("Root Growth", headingLevel+1));
            tags.Add(new AutoDocumentation.Paragraph("Roots grow downward through the soil profile and rate is determined by:",indent));
            foreach (IModel child in Apsim.Children(this, typeof(IModel)))
            {
                if (child.Name == "RootFrontVelocity")
                    child.Document(tags, headingLevel + 5, indent + 1);
            }

            if (TemperatureEffect.GetType() == typeof(Constant))
            {
                //Temp having no effect so no need to document
            }
            else
            {
                tags.Add(new AutoDocumentation.Paragraph("The RootFrontVelocity described above is influenced by temperature as:", indent));
                foreach (IModel child in Apsim.Children(this, typeof(IModel)))
                {
                    if (child.Name == "TemperatureEffect")
                        child.Document(tags, headingLevel + 5, indent + 1);
                }
            }

            tags.Add(new AutoDocumentation.Paragraph("The RootFrontVelocity is also influenced by the extension resistance posed by the soil, paramterised using the soil XF value", indent));

            tags.Add(new AutoDocumentation.Heading("Drymatter Demands", headingLevel + 1));
            // Describe biomass Demand
            tags.Add(new AutoDocumentation.Paragraph("100% of the DM demanded from the root is structural", indent));

            tags.Add(new AutoDocumentation.Paragraph("The daily DM demand from root is calculated as a proportion of total DM supply using:", indent));
            foreach (IModel child in Apsim.Children(this, typeof(IModel)))
            {
                if (child.Name == "PartitionFraction")
                    child.Document(tags, headingLevel + 5, indent + 1);
            }
            
            tags.Add(new AutoDocumentation.Heading("Nitrogen Demands", headingLevel + 1));
            tags.Add(new AutoDocumentation.Paragraph("The daily structural N demand from " + this.Name + " is the product of Total DM demand and a Nitrogen concentration of " + MinNconc * 100 + "%", indent));
            if (NitrogenDemandSwitch != null)
            {
                tags.Add(new AutoDocumentation.Paragraph("The Nitrogen demand swith is a multiplier applied to nitrogen demand so it can be turned off at certain phases.  For the " + Name + " Organ it is set as:", indent));
                foreach (IModel child in Apsim.Children(this, typeof(IModel)))
                {
                    if (child.Name == "NitrogenDemandSwitch")
                        child.Document(tags, headingLevel + 5, indent);
                }
            }

            tags.Add(new AutoDocumentation.Heading("Nitrogen Uptake", headingLevel + 1));
            tags.Add(new AutoDocumentation.Paragraph("potential N uptake by the root system is calculated for each soil layer that the roots have extended into.", indent));
            tags.Add(new AutoDocumentation.Paragraph("In each layer potential uptake is calculated as the product of the mineral nitrogen in the layer, a factor controllint the rate of extraction (kNO<sub>3</sub> and kNH<sub>4</sub>), the concentration of of N (ppm) and a soil moisture factor which decreases as the soil dries.", indent));
            tags.Add(new AutoDocumentation.Paragraph("Nitrogen uptake demand is limited to the maximum of potential uptake and the plants N demand.  Uptake N demand is then passed to the soil arbitrator which determines how much of their Nitrogen uptake demand each plant instance will be allowed to take up:", indent));

            tags.Add(new AutoDocumentation.Heading("Water Uptake", headingLevel + 1));
            tags.Add(new AutoDocumentation.Paragraph("Potential water uptake by the root system is calculated for each soil layer that the roots have extended into.", indent));
            tags.Add(new AutoDocumentation.Paragraph("In each layer potential uptake is calculated as the product of the available Water in the layer, and a factor controllint the rate of extraction (kl)", indent));
            tags.Add(new AutoDocumentation.Paragraph("The kl values are set in the soil and may be further modified by the crop.  are calculated in relation to root length density in each layer as :", indent));
            foreach (IModel child in Apsim.Children(this, typeof(IModel)))
                {
                    if (child.Name == "KLModifier" || child.Name == "KNO3" || child.Name == "KNH4")
                        child.Document(tags, headingLevel + 5, indent + 1);
                }
                
            // write Other functions.
            bool NonStandardFunctions = false;
            foreach (IModel child in Apsim.Children(this, typeof(IModel)))
            {
                if  ((child.GetType() != typeof(Memo))
                    | (child.GetType() != typeof(Biomass))
                    | (child.Name != "MaximumNConc")
                    | (child.Name != "MinimumNConc")
                    | (child.Name != "NitrogenDemandSwitch")
                    | (child.Name != "KLModifier")
                    | (child.Name != "SoilWaterEffect")
                    | (child.Name != "MaximumDailyUptake")
                    | (child.Name != "SenescenceRate") | (child.Name != "MaximumNConc")
                    | (child.Name != "TemperatureEffect")
                    | (child.Name != "MaximumRootDepth")
                    | (child.Name != "KLModifier")
                    | (child.Name != "RootFrontVelocity")
                    | (child.Name != "PartitionFraction"))
                    {
                        NonStandardFunctions = true;
                    }
            }

            if (NonStandardFunctions)
            {
                tags.Add(new AutoDocumentation.Heading("Other functionality", headingLevel + 1));
                tags.Add(new AutoDocumentation.Paragraph("In addition to the core functionality and parameterisation described above, the " + this.Name + " organ has additional functions used to provide paramters for core functions and create additional functionality", indent));
                foreach (IModel child in Apsim.Children(this, typeof(IModel)))
                {
                    if ((child.GetType() == typeof(Memo))
                    | (child is Biomass)
                    | (child.Name != "MaximumNConc")
                    | (child.Name != "MinimumNConc")
                    | (child.Name != "NitrogenDemandSwitch")
                    | (child.Name != "KLModifier")
                    | (child.Name != "SoilWaterEffect")
                    | (child.Name != "MaximumDailyUptake")
                    | (child.Name != "SenescenceRate") | (child.Name != "MaximumNConc")
                    | (child.Name != "TemperatureEffect")
                    | (child.Name != "MaximumRootDepth")
                    | (child.Name != "KLModifier")
                    | (child.Name != "RootFrontVelocity")
                    | (child.Name != "PartitionFraction"))
                    {//Already documented 
                    }
                    else
                    {
                        //tags.Add(new AutoDocumentation.Heading(child.Name, headingLevel + 2));
                        child.Document(tags, headingLevel + 2, indent + 1);
                    }
                }
            }
        }
    }
}
