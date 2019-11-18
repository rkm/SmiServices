package org.smi.extractorcl.test.fileUtils;

import junit.framework.TestCase;
import org.mockito.ArgumentCaptor;
import org.mockito.stubbing.Answer;
import org.smi.common.messaging.IProducerModel;
import org.smi.extractorcl.exceptions.LineProcessingException;
import org.smi.extractorcl.fileUtils.ExtractMessagesCsvHandler;
import org.smi.extractorcl.messages.ExtractionRequestInfoMessage;
import org.smi.extractorcl.messages.ExtractionRequestMessage;

import java.util.HashSet;
import java.util.UUID;

import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

public class ExtractImagesCsvHandlerTest extends TestCase {
	/**
	 * Test processing a simple single file.
	 *
	 * @throws Exception
	 */
	public void testProcessingSingleFile() throws Exception {
		IProducerModel extractRequestMessageProducerModel = mock(IProducerModel.class);
		IProducerModel extractRequestInfoMessageProducerModel = mock(IProducerModel.class);

		UUID uuid = UUID.randomUUID();
		ExtractMessagesCsvHandler handler = new ExtractMessagesCsvHandler(
				uuid,
				"MyProjectID",
				"MyProjectFolder",
				0,
				extractRequestMessageProducerModel,
				extractRequestInfoMessageProducerModel);

		handler.processHeader(new String[]{"SeriesInstanceUID"});
		handler.processLine(1, new String[]{"s1"});
		handler.processLine(2, new String[]{"s2"});
		handler.processLine(3, new String[]{"s3"});
		handler.processLine(4, new String[]{"s4"});
		handler.processLine(5, new String[]{"s5"});
		handler.finished();

		handler.sendMessages(true);

		ArgumentCaptor<Object> requestMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestMessageProducerModel).SendMessage(requestMessage.capture(), eq(""), eq(null));
		verifyNoMoreInteractions(extractRequestMessageProducerModel);

		ArgumentCaptor<Object> requestInfoMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestInfoMessageProducerModel).SendMessage(requestInfoMessage.capture(), eq(""), eq(null));
		verifyNoMoreInteractions(extractRequestInfoMessageProducerModel);

		// Check the messages had the correct details
		ExtractionRequestMessage erm = (ExtractionRequestMessage) requestMessage.getValue();
		assertEquals(uuid, erm.ExtractionJobIdentifier);
		assertEquals("MyProjectID", erm.ProjectNumber);
		assertEquals("MyProjectFolder", erm.ExtractionDirectory);
		assertEquals("SeriesInstanceUID", erm.KeyTag);
		assertEquals(5, erm.ExtractionIdentifiers.size());

		assertTrue(erm.ExtractionIdentifiers.contains("s1"));
		assertTrue(erm.ExtractionIdentifiers.contains("s2"));
		assertTrue(erm.ExtractionIdentifiers.contains("s3"));
		assertTrue(erm.ExtractionIdentifiers.contains("s4"));
		assertTrue(erm.ExtractionIdentifiers.contains("s5"));

		// Check the messages had the correct details
		ExtractionRequestInfoMessage erim = (ExtractionRequestInfoMessage) requestInfoMessage.getValue();
		assertEquals(uuid, erim.ExtractionJobIdentifier);
		assertEquals("MyProjectID", erim.ProjectNumber);
		assertEquals("MyProjectFolder", erim.ExtractionDirectory);
		assertEquals(5, erim.KeyValueCount);

	}

	/**
	 * Test processing a single file with duplicate series.
	 *
	 * @throws Exception
	 */
	public void testProcessingSingleFileWithDuplicateSeries() throws Exception {
		IProducerModel extractRequestMessageProducerModel = mock(IProducerModel.class);
		IProducerModel extractRequestInfoMessageProducerModel = mock(IProducerModel.class);

		UUID uuid = UUID.randomUUID();
		ExtractMessagesCsvHandler handler = new ExtractMessagesCsvHandler(
				uuid,
				"MyProjectID",
				"MyProjectFolder",
				0,
				extractRequestMessageProducerModel,
				extractRequestInfoMessageProducerModel);

		handler.processHeader(new String[]{"SeriesInstanceUID"});
		handler.processLine(1, new String[]{"s1"});
		handler.processLine(2, new String[]{"s2"});
		handler.processLine(3, new String[]{"s3"});
		handler.processLine(4, new String[]{"s4"});
		handler.processLine(5, new String[]{"s1"}); // This is the duplicate
		handler.finished();

		handler.sendMessages(true);

		ArgumentCaptor<Object> requestMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestMessageProducerModel).SendMessage(requestMessage.capture(), eq(""), eq(null));
		verifyNoMoreInteractions(extractRequestMessageProducerModel);

		ArgumentCaptor<Object> requestInfoMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestInfoMessageProducerModel).SendMessage(requestInfoMessage.capture(), eq(""), eq(null));
		verifyNoMoreInteractions(extractRequestInfoMessageProducerModel);

		// Check the messages had the correct details
		ExtractionRequestMessage erm = (ExtractionRequestMessage) requestMessage.getValue();
		assertEquals(uuid, erm.ExtractionJobIdentifier);
		assertEquals("MyProjectID", erm.ProjectNumber);
		assertEquals("MyProjectFolder", erm.ExtractionDirectory);
		assertEquals("SeriesInstanceUID", erm.KeyTag);
		assertEquals(4, erm.ExtractionIdentifiers.size());

		assertTrue(erm.ExtractionIdentifiers.contains("s1"));
		assertTrue(erm.ExtractionIdentifiers.contains("s2"));
		assertTrue(erm.ExtractionIdentifiers.contains("s3"));
		assertTrue(erm.ExtractionIdentifiers.contains("s4"));

		// Check the messages had the correct details
		ExtractionRequestInfoMessage erim = (ExtractionRequestInfoMessage) requestInfoMessage.getValue();
		assertEquals(uuid, erim.ExtractionJobIdentifier);
		assertEquals("MyProjectID", erim.ProjectNumber);
		assertEquals("MyProjectFolder", erim.ExtractionDirectory);
		assertEquals(4, erim.KeyValueCount);

	}

	/**
	 * Test processing a multiple files with duplicate series.
	 *
	 * @throws Exception
	 */
	public void testProcessingMultipleFilesWithDuplicateSeries() throws Exception {

		IProducerModel extractRequestMessageProducerModel = mock(IProducerModel.class);
		IProducerModel extractRequestInfoMessageProducerModel = mock(IProducerModel.class);

		UUID uuid = UUID.randomUUID();
		ExtractMessagesCsvHandler handler = new ExtractMessagesCsvHandler(
				uuid,
				"MyProjectID",
				"MyProjectFolder",
				0,
				extractRequestMessageProducerModel,
				extractRequestInfoMessageProducerModel);

		handler.processHeader(new String[]{"SeriesInstanceUID"});
		handler.processLine(1, new String[]{"s1"});
		handler.processLine(2, new String[]{"s2"});
		handler.processLine(3, new String[]{"s3"});
		handler.processLine(4, new String[]{"s4"});
		handler.finished();

		handler.processHeader(new String[]{"SeriesInstanceUID"});
		handler.processLine(1, new String[]{"s1"}); // Duplicate
		handler.processLine(2, new String[]{"s5"});
		handler.processLine(3, new String[]{"s6"});
		handler.processLine(4, new String[]{"s3"}); // Duplicate
		handler.finished();

		handler.sendMessages(true);

		ArgumentCaptor<Object> requestMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestMessageProducerModel).SendMessage(requestMessage.capture(), eq(""), eq(null));
		verifyNoMoreInteractions(extractRequestMessageProducerModel);

		ArgumentCaptor<Object> requestInfoMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestInfoMessageProducerModel).SendMessage(requestInfoMessage.capture(), eq(""), eq(null));
		verifyNoMoreInteractions(extractRequestInfoMessageProducerModel);

		// Check the messages had the correct details
		ExtractionRequestMessage erm = (ExtractionRequestMessage) requestMessage.getValue();

		assertEquals(uuid, erm.ExtractionJobIdentifier);
		assertEquals("MyProjectID", erm.ProjectNumber);
		assertEquals("MyProjectFolder", erm.ExtractionDirectory);
		assertEquals("SeriesInstanceUID", erm.KeyTag);
		assertEquals(6, erm.ExtractionIdentifiers.size());

		assertTrue(erm.ExtractionIdentifiers.contains("s1"));
		assertTrue(erm.ExtractionIdentifiers.contains("s2"));
		assertTrue(erm.ExtractionIdentifiers.contains("s3"));
		assertTrue(erm.ExtractionIdentifiers.contains("s4"));
		assertTrue(erm.ExtractionIdentifiers.contains("s5"));
		assertTrue(erm.ExtractionIdentifiers.contains("s6"));

		// Check the messages had the correct details
		ExtractionRequestInfoMessage erim = (ExtractionRequestInfoMessage) requestInfoMessage.getValue();
		assertEquals(uuid, erim.ExtractionJobIdentifier);
		assertEquals("MyProjectID", erim.ProjectNumber);
		assertEquals("MyProjectFolder", erim.ExtractionDirectory);
		assertEquals(6, erim.KeyValueCount);
	}

	public void testSeriesIndexOutOfBoundsForHeader() throws Exception {

		ExtractMessagesCsvHandler handler = new ExtractMessagesCsvHandler(
				UUID.randomUUID(),
				"MyProjectID",
				"MyProjectFolder",
				1,
				null,
				null);

		boolean producedException = false;
		String[] line = new String[]{"SeriesInstanceUID"};

		try {

			handler.processHeader(line);

		} catch (LineProcessingException lpe) {

			producedException = true;
			assertEquals("Line number should be 1", 1, lpe.getLineNumber());
			assertEquals(line, lpe.getLine());
			assertEquals("Error at line 1: Data header line has fewer columns (1) than the series ID column index (2)", lpe.getMessage());
		}

		assertTrue("Expected a LineProcessingException", producedException);
	}

	public void testSeriesIndexOutOfBoundsForData() throws Exception {

		ExtractMessagesCsvHandler handler = new ExtractMessagesCsvHandler(
				UUID.randomUUID(),
				"MyProjectID",
				"MyProjectFolder",
				1,
				null,
				null);

		boolean producedException = false;
		String[] line = null;

		try {

			handler.processHeader(new String[]{"otherItem", "SeriesInstanceUID"});
			line = new String[]{"s1"};
			handler.processLine(2, line);

		} catch (LineProcessingException lpe) {

			producedException = true;
			assertEquals("Line number should be 2", 2, lpe.getLineNumber());
			assertEquals(line, lpe.getLine());
			assertEquals("Error at line 2: Line has fewer columns (1) than the series ID column index (2)", lpe.getMessage());
		}

		assertTrue("Expected a LineProcessingException", producedException);
	}

	/**
	 * Test we correctly split a large set of identifiers into sub-messages
	 */
	public void testIdentifierSplit() throws LineProcessingException {

		int nIdentifiers = 25_500;
		int maxPerMessage = 1000;
		int expectedMessages = (nIdentifiers + maxPerMessage - 1) / maxPerMessage;

		IProducerModel extractRequestMessageProducerModel = mock(IProducerModel.class);
		IProducerModel extractRequestInfoMessageProducerModel = mock(IProducerModel.class);

		UUID extractionUid = UUID.randomUUID();

		ExtractMessagesCsvHandler handler = new ExtractMessagesCsvHandler(
				extractionUid,
				"MyProjectID",
				"MyProjectFolder",
				0,
				extractRequestMessageProducerModel,
				extractRequestInfoMessageProducerModel);

		handler.processHeader(new String[]{"SeriesInstanceUID"});

		HashSet<String> expected = new HashSet<>();
		for (int i = 0; i < nIdentifiers; ++i) {
			String id = "id" + i;
			handler.processLine(i + 1, new String[]{id});
			expected.add(id);
		}

		handler.finished();

		HashSet<String> captured = new HashSet<>();

		doAnswer(
				(Answer<Void>) invocation -> {
					captured.addAll(((ExtractionRequestMessage) invocation.getArguments()[0]).ExtractionIdentifiers);
					return null;
				})
				.when(extractRequestMessageProducerModel)
				.SendMessage(any(), eq(""), eq(null));

		handler.sendMessages(true, maxPerMessage);

		verify(extractRequestMessageProducerModel, times(expectedMessages)).SendMessage(any(), eq(""), eq(null));

		ArgumentCaptor<Object> requestInfoMessage = ArgumentCaptor.forClass(Object.class);
		verify(extractRequestInfoMessageProducerModel).SendMessage(requestInfoMessage.capture(), eq(""), eq(null));

		assertEquals("The correct number of identifiers were sent", expected.size(), captured.size());
		assertTrue("Set are equal", expected.equals(captured));
	}
}
