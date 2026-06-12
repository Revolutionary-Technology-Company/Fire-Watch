# DOCUMENT 3: EDWARDS FIREWORKS WORKSTATION INTEGRATION GUIDE
**System:** Genetec Edwards FireWatch Bridge Service  
**Classification:** Supplemental Hospital Life-Safety Systems Middleware  
**Target Audience:** Fire Alarm Systems Integrators, Edwards Life-Safety Certified Engineers

---

## 1. Network Driver Provisioning on FireWorks
The middleware pushes event triggers over an open raw TCP stream socket. The receiving Edwards FireWorks computer must be configured to process this data stream.

### 1.1 Creating the External Text Receiver Driver
1. Open the **Edwards FireWorks System Configuration Utility**.
2. Navigate to the **External Systems / Driver Interoperability Options** manager window.
3. Add a new device instance and select the **Generic Text/ASCII System Receiver Driver**.
4. Configure the communication layer network bounds:
   * Set IP binding listener target matching the FireWorks host system interface (e.g., `10.100.50.12`).
   * Allocate an unassigned inbound server port (Standard: `2323`).

---

## 2. Defining Stream Delimiter Rules
To properly route incoming bridge commands into active system responses, configure the FireWorks driver parser string template to split the message tokens using these definitions:

### 2.1 Driver Framing Character Codes
* **Message Framing Start Byte:** Configure to intercept `ASCII Character 2` (`[STX]`).
* **Message Framing Terminating Byte:** Configure to intercept `ASCII Character 3` (`[ETX]`).
* **Data Field Delimiter Token:** Configure a standard `,` (Comma) separation marker value.

### 2.2 Field Order Index Mapping
Map the sequential elements arriving in the text stream into the corresponding internal variables inside the Edwards database:
1. **Field 1:** Message Flag Status Evaluation String (`ALARM` or `STATUS`)
2. **Field 2:** Target System Panel Control Node Identifier (`EdwardsNode` parameter)
3. **Field 3:** Targeted Device Panel Alarm Group Identifier (`EdwardsZone` parameter)
4. **Field 4:** Descriptive Operator Notification Information Text (`PhysicalRoom` string data)
5. **Field 5:** Exact Incident Origin System Event Log Timestamp Data

---

## 3. Configuring the Supervision Heartbeat Fault Window
The middleware transmits an automated heartbeat packet (`\x02STATUS,BRIDGE,NORMAL,HEARTBEAT OK\x03\x0D`) every 60 seconds to prove the link is live.

### 3.1 Creating the Network Loss Trouble Monitor
1. Inside the FireWorks configuration tree, create a pseudo tracking device bound to the `BRIDGE` zone label text.
2. Set a **Supervision Watchdog Disconnection Timeout Timer** bound to exactly **120 seconds** (this allows a buffer for one missed packet).
3. Configure the response matrix rule: If no text string containing the `HEARTBEAT OK` pattern arrives within the 120-second window, the system must trigger a yellow **"Supplemental Video Monitor Communications Trouble Alert"** on the operators' console terminal displays.
