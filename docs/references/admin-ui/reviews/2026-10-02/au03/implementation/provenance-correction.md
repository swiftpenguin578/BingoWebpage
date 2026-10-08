# AU03 patch ownership correction

The original before-to-current capture inadvertently included a concurrent AU15
Fetch-now PRODUCT_REQUIREMENTS.md hunk. The before/ copy lacks that hunk. The
implementer's recorded authority edit only inserted four creation-retry lines
near line 70 of this document; implement.py contains no contract-document edits.
The AU15 hunk was not authored as AU03 work. Its exact concurrent author is not
independently verified by these filesystem artifacts; the orchestrator owns routing.

No live repository source or code was changed for this correction. All 20 full-file
source hashes remain unchanged, including PRODUCT_REQUIREMENTS.md, which contains
both the AU03 and unrelated concurrent edit. A full-file hash proves identity, not
exclusive ownership. Ticket ownership is defined by the corrected au03.patch.

The original capture is retained as au03-with-concurrent-capture.patch, SHA-256
3e2af7360684362493aa7a536fe98631f165ebb4087403171589666016a596c8.
excluded-concurrent-au15.patch isolates the unrelated edit. The original before/
copies and before.sha256 are unchanged. No tests need rerunning because no source
changed; existing executed evidence remains applicable.

Corrected pre-remediation au03.patch SHA-256: fd617d662dfa2160cd8b4732340827e6e874cbe12390712944f6f0bb097d4ec4

The later named reset remediation extends the patch and manifest; current identities are in handoff.md. The concurrent AU15 exclusion remains unchanged.
