using System;
using System.Collections.Generic;
using System.Text;
using Models.Core;
using Models.PMF.Functions;
using Models.PMF.Phen;
using Models.PMF.Interfaces;
using System.Xml;
using System.Xml.Serialization;

namespace Models.PMF.Organs
{
    /// <summary>
    /// This organ uses a generic model for plant reproductive components.  Yield is calculated from its components in terms of organ number and size (for example, grain number and grain size).  
    /// </summary>
    [Serializable]
    public class ReproductiveOrgan : BaseOrgan, Reproductive, AboveGround
    {
        #region Parameter Input Classes
        /// <summary>The phenology</summary>
        [Link]
        protected Phenology Phenology = null;
        /// <summary>The water content</summary>
        [Link]
        [Units("g/g")]
        [Description("Water content used to calculate a fresh weight.")]
        IFunction WaterContent = null;
        
        /// <summary>The Maximum potential size of individual grains</summary>
        [Link]
        [Units("g/grain")]
        IFunction MaximumPotentialGrainSize = null;
        
        /// <summary>The number function</summary>
        [Link]
        [Units("/m2")]
        IFunction NumberFunction = null;
        /// <summary>The n filling rate</summary>
        [Link]
        [Units("g/m2/d")]
        IFunction NFillingRate = null;
        /// <summary>The maximum n conc</summary>
        [Link]
        [Units("g/g")]
        IFunction MaximumNConc = null;
        /// <summary>The minimum n conc</summary>
        [Link]
        [Units("g/g")]
        IFunction MinimumNConc = null;
        /// <summary>The dm demand function</summary>
        [Link]
        [Units("g/m2/d")]
        IFunction DMDemandFunction = null;
        #endregion

        #region Class Fields
        /// <summary>The ripe stage</summary>
        public string RipeStage = "";
        /// <summary>The _ ready for harvest</summary>
        protected bool _ReadyForHarvest = false;
        /// <summary>The potential dm allocation</summary>
        private double PotentialDMAllocation = 0;
        #endregion

        #region Class Properties

        /// <summary>The number</summary>
        [XmlIgnore]
        [Units("/m^2")]
        public double Number { get; set; }

        /// <summary>The Maximum potential size of grains</summary>
        [XmlIgnore]
        [Units("/m^2")]
        public double MaximumSize { get; set; }

        /// <summary>Gets the live f wt.</summary>
        /// <value>The live f wt.</value>
        [Units("g/m^2")]
        public double LiveFWt
        {
            get
            {
                if (WaterContent != null)
                    return Live.Wt / (1 - WaterContent.Value);
                else
                    return 0.0;
            }
        }

        /// <summary>Gets the size.</summary>
        /// <value>The size.</value>
        [Units("g")]
        public double Size
        {
            get
            {
                if (Number > 0)
                    return Live.Wt / Number;
                else
                    return 0;
            }
        }

        /// <summary>Gets the size of the f.</summary>
        /// <value>The size of the f.</value>
        [Units("g")]
        private double FSize
        {
            get
            {
                if (Number > 0)
                {
                    if (WaterContent != null)
                        return (Live.Wt / Number) / (1 - WaterContent.Value);
                    else
                        return 0.0;
                }
                else
                    return 0;
            }
        }

        /// <summary>Gets the ready for harvest.</summary>
        /// <value>The ready for harvest.</value>
        public int ReadyForHarvest
        {
            get
            {
                if (_ReadyForHarvest)
                    return 1;
                else
                    return 0;
            }
        }
        #endregion

        #region Functions

                /// <summary>Event from sequencer telling us to do our potential growth.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoPotentialPlantGrowth")]
        protected void OnDoPotentialPlantGrowth(object sender, EventArgs e)
        {
            if (Plant.IsAlive)
            {
                Number = NumberFunction.Value;
                MaximumSize = MaximumPotentialGrainSize.Value;
            }
        }

        /// <summary>Called when crop is ending</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="data">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantSowing")]
        private void OnPlantSowing(object sender, SowPlant2Type data)
        {
            if (data.Plant == Plant)
                Clear();
        }

        /// <summary>
        /// Execute harvest logic for reproductive organ
        /// </summary>
        public override void DoHarvest()
        {
                double YieldDW = (Live.Wt + Dead.Wt);

                Summary.WriteMessage(this, "Harvesting " + Name + " from " + Plant.Name);
                Summary.WriteMessage(this, " Yield DWt: " + YieldDW.ToString("f2") + " (g/m^2)");
                Summary.WriteMessage(this, " Size: " + Size.ToString("f2") + " (g)");
                Summary.WriteMessage(this, " Number: " + Number.ToString("f2") + " (/m^2)");

                Live.Clear();
                Dead.Clear();
                Number = 0;
                _ReadyForHarvest = false;
        }
        #endregion

        #region Event handlers

        /// <summary>Called when [simulation commencing].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            Clear();
        }

        /// <summary>Called when crop is being cut.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Cutting")]
        private void OnCutting(object sender, EventArgs e)
        {
            if (sender == Plant)
            {
                Summary.WriteMessage(this, "Cutting " + Name + " from " + Plant.Name);

                Live.Clear();
                Dead.Clear();
                Number = 0;
                _ReadyForHarvest = false;
            }
        }
        #endregion

        #region Arbitrator methods
        /// <summary>Does the nutrient allocations.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoActualPlantGrowth")]
        private void OnDoActualPlantGrowth(object sender, EventArgs e)
        {
            if (Phenology.OnDayOf(RipeStage))
                _ReadyForHarvest = true;
        }
        /// <summary>Gets or sets the dm demand.</summary>
        /// <value>The dm demand.</value>
        public override BiomassPoolType DMDemand
        {
            get
            {
                return new BiomassPoolType { Structural = DMDemandFunction.Value };
            }
        }
        /// <summary>Sets the dm potential allocation.</summary>
        /// <value>The dm potential allocation.</value>
        /// <exception cref="System.Exception">Invalid allocation of potential DM in + Name</exception>
        public override BiomassPoolType DMPotentialAllocation
        {
            set
            {
                if (DMDemand.Structural == 0)
                    if (value.Structural < 0.000000000001) { }//All OK
                    else
                        throw new Exception("Invalid allocation of potential DM in" + Name);
                PotentialDMAllocation = value.Structural;
                // PotentialDailyGrowth = value.Structural;
            }
        }
        /// <summary>Sets the dm allocation.</summary>
        /// <value>The dm allocation.</value>
        public override BiomassAllocationType DMAllocation
        { set { Live.StructuralWt += value.Structural; } }
        /// <summary>Gets or sets the n demand.</summary>
        /// <value>The n demand.</value>
        public override BiomassPoolType NDemand
        {
            get
            {
                double demand = NFillingRate.Value;
                demand = Math.Min(demand, MaximumNConc.Value * PotentialDMAllocation);
                return new BiomassPoolType { Structural = demand };
            }
        }
        /// <summary>Sets the n allocation.</summary>
        /// <value>The n allocation.</value>
        public override BiomassAllocationType NAllocation
        {
            set
            {
                Live.StructuralN += value.Structural;
            }
        }
        /// <summary>Gets or sets the maximum nconc.</summary>
        /// <value>The maximum nconc.</value>
        public override double MaxNconc
        {
            get
            {
                return MaximumNConc.Value;
            }
        }
        /// <summary>Gets or sets the minimum nconc.</summary>
        /// <value>The minimum nconc.</value>
        public override double MinNconc
        {
            get
            {
                return MinimumNConc.Value;
            }
        }
        #endregion
    }
}
